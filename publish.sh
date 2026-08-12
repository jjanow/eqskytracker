#!/usr/bin/env bash
# Builds self-contained, single-file executables that run on a target
# machine with nothing pre-installed -- no .NET runtime, no Python. Each
# output folder under publish/<rid>/ is a complete, portable copy of the app;
# zip it up and hand it to someone, or copy it to a USB stick.
#
# Usage: ./publish.sh [runtime-id ...]
#   Defaults to win-x64, linux-x64, and osx-arm64 if none are given.
#   Other valid values: linux-arm64, osx-x64, win-arm64.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

rids=("$@")
if [ ${#rids[@]} -eq 0 ]; then
    rids=(win-x64 linux-x64 osx-arm64)
fi

for rid in "${rids[@]}"; do
    for proj in EqSkyTracker.Gui EqSkyTracker.Cli; do
        out="publish/$rid/$proj"
        echo "Publishing $proj for $rid -> $out"
        dotnet publish "src/$proj/$proj.csproj" \
            -c Release -r "$rid" --self-contained true \
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
            -p:DebugType=none \
            -o "$out"
        # .NET's own debug symbols are already suppressed by DebugType=none
        # above; these are *.pdb files some third-party native-dependency
        # NuGet packages (e.g. SkiaSharp, HarfBuzzSharp) ship as plain
        # content alongside their native libs -- strip them too, since
        # they're only useful for debugging those libraries' native code,
        # not this app, and they dwarf everything else in the folder.
        find "$out" -name '*.pdb' -delete
    done
done

echo
echo "Done. Each publish/<rid>/ folder is self-contained -- copy the whole"
echo "folder to the target machine and run the .exe (Windows) or executable"
echo "(Linux/macOS) inside it directly."
