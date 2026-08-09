#!/usr/bin/env bash
# Single-command GUI launcher: no install step needed (the package has zero
# third-party dependencies, so running it in place from the repo works
# as-is). This always opens the GUI -- for terminal flags like --list-chars
# or --dir, use `python3 -m eqskytracker` directly instead.
set -euo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"
exec python3 -m eqskytracker --gui
