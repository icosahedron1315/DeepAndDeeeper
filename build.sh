#!/bin/bash

# DeepAndDeeper Build Script
# Usage: ./build.sh [1.5|1.6|all|clean]

cd "$(dirname "$0")/Source"

case "$1" in
    "1.5")
        echo "Building for RimWorld 1.5..."
        dotnet build DeepAndDeeper.csproj -c Release
        echo "Build complete: v1.5/Assemblies/DeepAndDeeper.dll"
        ;;
    "1.6")
        echo "Building for RimWorld 1.6..."
        dotnet build DeepAndDeeper.csproj -c Release1.6
        echo "Build complete: v1.6r2/Assemblies/DeepAndDeeper.dll"
        ;;
    "all")
        echo "Building for all RimWorld versions..."
        echo "Building 1.5..."
        dotnet build DeepAndDeeper.csproj -c Release
        echo "Building 1.6..."
        dotnet build DeepAndDeeper.csproj -c Release1.6
        echo "All builds complete!"
        ;;
    "clean")
        echo "Cleaning build artifacts..."
        dotnet clean DeepAndDeeper.csproj
        rm -rf obj/
        echo "Clean complete!"
        ;;
    *)
        echo "DeepAndDeeper Build Script"
        echo "Usage: $0 [1.5|1.6|all|clean]"
        echo ""
        echo "Commands:"
        echo "  1.5    - Build for RimWorld 1.5"
        echo "  1.6    - Build for RimWorld 1.6"
        echo "  all    - Build for both versions"
        echo "  clean  - Clean build artifacts"
        echo ""
        echo "Examples:"
        echo "  $0 1.6     # Build for RimWorld 1.6"
        echo "  $0 all     # Build for both versions"
        ;;
esac