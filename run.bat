@echo off
rem Windows launcher analog to run.sh: builds and runs the GUI via the .NET
rem SDK (`dotnet`), so it needs the SDK installed here -- same tradeoff the
rem old Python version had with needing python installed. For a build that
rem runs on a machine with NOTHING pre-installed (no .NET, no Python), run
rem publish.bat once and hand out the resulting publish\<rid>\ folder
rem instead -- that .exe needs no install step at all. For terminal flags
rem like --list-chars or --dir, use `dotnet run --project src\EqSkyTracker.Cli --`
rem directly instead.
cd /d "%~dp0"
dotnet run --project src\EqSkyTracker.Gui -c Release -- %*
