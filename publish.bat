@echo off
rem Windows analog to publish.sh: builds self-contained, single-file
rem executables that run on a target machine with nothing pre-installed --
rem no .NET runtime, no Python. Each output folder under publish\<rid>\ is a
rem complete, portable copy of the app.
rem
rem Usage: publish.bat [runtime-id ...]
rem   Defaults to win-x64, linux-x64, and osx-arm64 if none are given.
cd /d "%~dp0"

set RIDS=%*
if "%RIDS%"=="" set RIDS=win-x64 linux-x64 osx-arm64

for %%R in (%RIDS%) do (
    for %%P in (EqSkyTracker.Gui EqSkyTracker.Cli) do (
        echo Publishing %%P for %%R -^> publish\%%R\%%P
        dotnet publish "src\%%P\%%P.csproj" ^
            -c Release -r %%R --self-contained true ^
            -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true ^
            -p:DebugType=none ^
            -o "publish\%%R\%%P"
        del /q "publish\%%R\%%P\*.pdb" 2>nul
    )
)

echo.
echo Done. Each publish\^<rid^>\ folder is self-contained -- copy the whole
echo folder to the target machine and run the .exe inside it directly.
