#!/usr/bin/env bash
# Single-command GUI launcher for a checked-out source tree: builds and runs
# the GUI via the .NET SDK (`dotnet`), so it needs the SDK installed here --
# same tradeoff the old Python version had with needing python3 installed.
# For a build that runs on a machine with NOTHING pre-installed (no .NET, no
# Python), run ./publish.sh once and hand out the resulting publish/<rid>/
# folder instead. For terminal flags like --list-chars or --dir, use
# `dotnet run --project src/EqSkyTracker.Cli --` directly instead.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
exec dotnet run --project src/EqSkyTracker.Gui -c Release -- "$@"
