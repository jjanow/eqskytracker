@echo off
rem Windows analog to publish.sh: builds self-contained executables that run
rem on a target machine with nothing pre-installed -- no .NET runtime, no
rem Python. Each output folder under publish\<rid>\ is a complete, portable
rem copy of the app.
rem
rem The Cli build is single-file (one executable, easy to hand someone
rem directly). The Gui build is left as loose multi-file output instead:
rem it's packaged for distribution with Velopack, whose delta updates work
rem by diffing individual files between releases -- bundling everything into
rem one file would collapse that into a single, mostly-unmatchable blob and
rem defeat delta updates almost entirely.
rem
rem Usage: publish.bat [runtime-id ...]
rem   Defaults to win-x64, linux-x64, and osx-arm64 if none are given.
setlocal enabledelayedexpansion
cd /d "%~dp0"

set RIDS=%*
if "%RIDS%"=="" set RIDS=win-x64 linux-x64 osx-arm64

for %%R in (%RIDS%) do (
    for %%P in (EqSkyTracker.Gui EqSkyTracker.Cli) do (
        set "SINGLE_FILE_ARGS="
        if "%%P"=="EqSkyTracker.Cli" set "SINGLE_FILE_ARGS=-p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true"
        echo Publishing %%P for %%R -^> publish\%%R\%%P
        dotnet publish "src\%%P\%%P.csproj" ^
            -c Release -r %%R --self-contained true ^
            !SINGLE_FILE_ARGS! ^
            -p:DebugType=none ^
            -o "publish\%%R\%%P"
        del /q "publish\%%R\%%P\*.pdb" 2>nul
    )
)

echo.
echo Done. Each publish\^<rid^>\ folder is self-contained -- copy the whole
echo folder to the target machine and run the .exe inside it directly.
