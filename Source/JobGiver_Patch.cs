using System.Linq;
using RimWorld;
using Verse;
using Verse.AI;

namespace Shashlichnik
{
    public static class JobGiver_Patch
    {
        public static string[] overridingJobDefNames = new string[] { "LayDown", "Ingest" };

        private static void TryOverrideJob(Pawn pawn, ref Job job)
        {
            // Log.Message($"job is [{job?.def?.defName}]");
            if (job?.def?.defName is { } val
                && overridingJobDefNames.Contains(val))
            {
                var caveComp = pawn.Map.GetComponent<CaveMapComponent>();
                if (caveComp != null && caveComp.caveExit != null && caveComp.caveExit.Spawned &&
                    caveComp.caveExit.exitIfNoJob)
                {
                    // Check if this is a hunger-related job and pawn can't find food in the cave
                    if (val == "Ingest" && !CanFindFoodInCave(pawn, caveComp))
                    {
                        Log.Message($"Detected hunger but no food available in cave. Redirecting pawn to exit cave.");
                        var oldjob = job;
                        JobMaker.ReturnToPool(oldjob); // Return the old job to the pool
                        // make a new one
                        job = JobMaker.MakeJob(JobDefOf.EnterPortal, caveComp.caveExit);
                    }
                    // Check if this is sleeping on floor (original functionality)
                    else if (val == "LayDown")
                    {
                        Log.Message($"Detected overridable job [{val}]. Redirecting the pawn to exit cave instead.");
                        var oldjob = job;
                        JobMaker.ReturnToPool(oldjob); // Return the old job to the pool
                        // make a new one
                        job = JobMaker.MakeJob(JobDefOf.EnterPortal, caveComp.caveExit);
                    }
                }
            }
        }

        private static bool CanFindFoodInCave(Pawn pawn, CaveMapComponent caveComp)
        {
            Map caveMap = caveComp.caveExit.Map;
            
            // Look for any food sources in the cave that the pawn can access
            var foodSources = caveMap.listerThings.ThingsInGroup(ThingRequestGroup.FoodSourceNotPlantOrTree)
                .Where(food => food.def.IsNutritionGivingIngestible && 
                              !food.IsForbidden(pawn) && 
                              pawn.CanReach(food, PathEndMode.OnCell, Danger.Deadly));
            
            return foodSources.Any();
        }

        // patch return result
        [HarmonyLib.HarmonyPatch(typeof(JobGiver_GetRest), "TryGiveJob")]
        internal static class GetRest_TryGiveJob_Patch
        {
            public static void Postfix(Pawn pawn, ref Job __result)
            {
                TryOverrideJob(pawn, ref __result);
            }
        }

        // patch return result for food (hunger)
        [HarmonyLib.HarmonyPatch(typeof(JobGiver_GetFood), "TryGiveJob")]
        internal static class GetFood_TryGiveJob_Patch
        {
            public static void Postfix(Pawn pawn, ref Job __result)
            {
                // If no food job was created, check if we should exit cave due to hunger
                if (__result == null)
                {
                    var caveComp = pawn.Map.GetComponent<CaveMapComponent>();
                    if (caveComp != null && caveComp.caveExit != null && caveComp.caveExit.Spawned &&
                        caveComp.caveExit.exitIfNoJob && !CanFindFoodInCave(pawn, caveComp))
                    {
                        Log.Message($"{pawn.Name} found no food in cave. Giving exit job.");
                        __result = JobMaker.MakeJob(JobDefOf.EnterPortal, caveComp.caveExit);
                    }
                }
                
                TryOverrideJob(pawn, ref __result);
            }
        }
    }
}