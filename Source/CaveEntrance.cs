using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse.Sound;
using Verse;
using Shashlichnik;

namespace Shashlichnik
{
    [StaticConstructorOnStartup]
    public class CaveEntrance : MapPortal
    {

        public override int? OverrideGraphicIndex => Mathf.FloorToInt((tickToOpenConst - ticksToOpen) / ((tickToOpenConst / 5) + 200));

        public bool IsCollapsing
        {
            get
            {
                return CaveMapComponent?.IsCollapsing ?? this.Map.GetComponent<CaveMapComponent>()?.IsCollapsing ?? false;
            }
        }
        private static IEnumerable<MapGeneratorDef> GeneratorsByLevel
        {
            get
            {
                yield return DefsOf.ShashlichnikUnderground;
                yield return DefsOf.ShashlichnikUndergroundLvl2;
                yield return DefsOf.ShashlichnikUndergroundLvl3;
                yield return DefsOf.ShashlichnikUndergroundLvl4;
            }
        }
        protected MapGeneratorDef MapGeneratorDef
        {
            get
            {
                return GeneratorsByLevel.ElementAtOrDefault(Level) ?? GeneratorsByLevel.Last(); 
            }
        }
        public int Level => Map?.GetComponent<CaveMapComponent>()?.caveEntrance?.Level + 1 ?? 0;
        public int CollapseTick => CaveMapComponent?.collapseTick ?? -999999;
        private CaveMapComponent caveMapComponent;
        public CaveMapComponent CaveMapComponent
        {
            get
            {
                return caveMapComponent ??= GetOtherMap()?.GetComponent<CaveMapComponent>();
            }
        }

        public int CollapseStage
        {
            get
            {
                if (CollapseTick - Find.TickManager.TicksGame >= 3600)
                {
                    return 1;
                }
                return 2;
            }
        }



        protected override Texture2D EnterTex
        {
            get
            {
                return enterTex;
            }
        }

#if v16
        public override string EnterString
#else
        public override string EnterCommandString
#endif
        {
            get
            {
                return "ShashlichnikEnterCave".Translate();
            }
        }
        public int TicksToOpen
        {
            get
            {
                return ticksToOpen;
            }
            set
            {
                if (ticksToOpen <= 0)
                {
                    return;
                }
                ticksToOpen = value;
                tickOpened = Find.TickManager.TicksGame;
                if (value <= 0 && cave == null)
                {
                    GenerateUndercave();
                }
                if (value <= 0)
                {
                    Map.GetComponent<CaveEntranceTracker>().Notify_OpenedAt(Position);
                }
            }
        }
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tickOpened, nameof(tickOpened), 0, false);
            Scribe_References.Look(ref cave, nameof(cave), false);
            Scribe_References.Look(ref caveExit, nameof(caveExit), false);
            Scribe_Values.Look(ref ticksToOpen, nameof(ticksToOpen));
            Scribe_Values.Look(ref autoEnter, nameof(autoEnter), false);
            BackwardCompatibility();
        }
#pragma warning disable 0618
        private void BackwardCompatibility()
        {

            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                Scribe_Values.Look(ref collapseTick, nameof(collapseTick), 0, false);
                Scribe_Values.Look(ref isCollapsing, nameof(isCollapsing), false, false);
            }
        }
#pragma warning restore 0618

#if v16
        protected override void Tick()
#else
        public override void Tick()
#endif
        {
            base.Tick();
            if (IsCollapsing)
            {
                if (CollapseStage == 1)
                {
                    if (collapseEffecter1 == null)
                    {
                        collapseEffecter1 = DefsOf.ShashlichnikCaveEntranceCollapseStage1?.Spawn(this, base.Map, 1f);
                    }
                }
                else if (CollapseStage == 2)
                {
                    if (collapseSustainer == null && Mod.Settings.AnomalyEffectsEnabled)
                    {
                        collapseSustainer = SoundDefOf.PitGateCollapsing?.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
                    }
                    collapseSustainer?.Maintain();
                    if (collapseEffecter2 == null)
                    {
                        collapseEffecter2 = DefsOf.ShashlichnikCaveEntranceCollapseStage2?.Spawn(this, base.Map, 1f);
                    }
                    if (Find.CurrentMap == base.Map && Rand.MTBEventOccurs(2f, 60f, 1f))
                    {
                        Find.CameraDriver.shaker.DoShake(0.2f);
                    }
                }
                Effecter effecter = collapseEffecter1;
                if (effecter != null)
                {
                    effecter.EffectTick(this, this);
                }
                Effecter effecter2 = collapseEffecter2;
                if (effecter2 != null)
                {
                    effecter2.EffectTick(this, this);
                }
                return;
            }

        }
        private void Collapse()
        {
            collapseSustainer?.End();
            Effecter effecter = collapseEffecter2;
            if (effecter != null)
            {
                effecter.Cleanup();
            }
            collapseEffecter2 = null;
            Effecter effecter2 = collapseEffecter1;
            if (effecter2 != null)
            {
                effecter2.Cleanup();
            }
            collapseEffecter1 = null;
            if (Mod.Settings.AnomalyEffectsEnabled)
            {
                EffecterDefOf.PitGateAboveGroundCollapsed?.Spawn(base.Position, base.Map, 1f);
            }
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = base.Map;
            Collapse();
            if (!IsCollapsing)
            {
                BeginCollapsing();
            }
            base.Destroy(mode);
            EffecterDefOf.ImpactDustCloud?.Spawn(base.Position, map, 1f).Cleanup();
            if (caveExit != null && !caveExit.Destroyed)
            {
                caveExit.Destroy(mode);
            }
        }

        public void BeginCollapsing(bool silent = false, int? forcedCollapseTick = null)
        {
            if (!IsCollapsing)
            {
                CaveMapComponent?.BeginCollapsing(this, silent, forcedCollapseTick);
            }
        }
#if !v16
        public static CaveEntrance currentlyGeneratingPortal;
#endif
        public void GenerateUndercave()
        {
            var mapSize = Mod.Settings.mapSize;
#if v16
            PocketMapUtility.
#endif
                currentlyGeneratingPortal = this;
            cave = PocketMapUtility.GeneratePocketMap(new IntVec3(mapSize, 1, mapSize), MapGeneratorDef, null, base.Map);
            caveExit = cave.listerThings.ThingsOfDef(DefsOf.ShashlichnikCaveExit).First() as CaveExit;
            caveExit.caveEntrance = this;
#if v16
            PocketMapUtility.
#endif
                currentlyGeneratingPortal = null;
        }

        public override bool IsEnterable(out string reason)
        {
            if (TicksToOpen > 0)
            {
                reason = "ShashlichnikCaveNotDugYet".Translate();
                return false;
            }

            reason = "";
            return true;
        }

        public override string GetInspectString()
        {
            var sb = new StringBuilder(base.GetInspectString());
            if (TicksToOpen > 0)
            {
                sb.AppendLineIfNotEmpty();
                sb.Append("WorkLeft".Translate() + ": " + ((float)ticksToOpen).ToStringWorkAmount());
            }
            return sb.ToString();
        }

        public override Map GetOtherMap()
        {
            if (cave == null && TicksToOpen <= 0)
            {
                GenerateUndercave();
            }
            return cave;
        }

        public override IntVec3 GetDestinationLocation()
        {
            var caveExit = this.caveExit;
            if (caveExit == null)
            {
                return IntVec3.Invalid;
            }
            return caveExit.Position;
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            if (TicksToOpen > 0)
            {
                return;
            }
            var collapseChance = 2 * (MaxHitPoints - HitPoints) / MaxHitPoints;
            if (Rand.Chance(collapseChance))
            {
                BeginCollapsing();
            }
        }

        public override void OnEntered(Pawn pawn)
        {
            base.OnEntered(pawn);
            if (Find.CurrentMap == base.Map)
            {
                SoundDefOf.TraversePitGate?.PlayOneShot(this);
                return;
            }
            if (Find.CurrentMap == caveExit.Map)
            {
                SoundDefOf.TraversePitGate?.PlayOneShot(caveExit);
            }
        }
        private static Texture2D enterTex = ContentFinder<Texture2D>.Get("UI/Buttons/Drop");
        private static Texture2D viewCaveTex = ContentFinder<Texture2D>.Get("UI/Widgets/Search");
        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (Gizmo gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (cave != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "ViewUndercave".Translate(),
                    defaultDesc = "ViewUndercaveDesc".Translate(),
                    icon = viewCaveTex,
                    action = delegate ()
                    {
                        CameraJumper.TryJumpAndSelect(caveExit, CameraJumper.MovementMode.Pan);
                    }
                };
                yield return new Command_Toggle
                {
                    defaultLabel = "ShashlichnikAutoEnter".Translate(),
                    defaultDesc = "ShashlichnikAutoEnterDesc".Translate(),
                    icon = enterTex,
                    isActive = () => autoEnter,
                    toggleAction = () => autoEnter = !autoEnter
                };
            }
            if (ticksToOpen > 0 && DebugSettings.ShowDevGizmos)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Finish digging",
                    action = () => TicksToOpen = 0
                };
            }
            if (IsCollapsing)
            {
                yield break;
            }
            if (!DebugSettings.ShowDevGizmos)
            {
                yield break;
            }
            if (cave != null)
            {
                yield return new Command_Action
                {
                    defaultLabel = "DEV: Collapse cave",
                    action = new Action(() => BeginCollapsing())
                };
            }
        }


        public int tickOpened = -999999;
        [Obsolete($"Use {nameof(CaveMapComponent)}.{nameof(Shashlichnik.CaveMapComponent.collapseTick)} instead")]
        internal int collapseTick = -999999;
        private int ticksToOpen = tickToOpenConst;
        public const int tickToOpenConst = GenDate.TicksPerHour * 36;
        [Obsolete($"Use {nameof(CaveMapComponent)}.{nameof(Shashlichnik.CaveMapComponent.IsCollapsing)} instead")]
        internal bool isCollapsing;
        public Map cave;
        public CaveExit caveExit;
        internal Sustainer collapseSustainer;
        internal Effecter collapseEffecter1;
        internal Effecter collapseEffecter2;
        public bool autoEnter;
    }
}