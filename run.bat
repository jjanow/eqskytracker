@echo off
rem Windows launcher analog to run.sh: no install step needed (the package
rem has zero third-party dependencies, so running it in place from the repo
rem works as-is). This always opens the GUI -- for terminal flags like
rem --list-chars or --dir, use `python -m eqskytracker` directly instead.
cd /d "%~dp0"

where py >nul 2>nul
if %errorlevel%==0 (
    py -3 -m eqskytracker --gui
) else (
    python -m eqskytracker --gui
)
