#!/usr/bin/env bash
# macOS double-click launcher (Finder won't exec a plain .sh, but honors
# the .command extension). Just delegates to run.sh so there's one
# source of truth for the actual launch logic.
cd "$(dirname "${BASH_SOURCE[0]}")"
exec ./run.sh
