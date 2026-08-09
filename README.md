# eqskytracker

Tracks your progress toward unlocking all classes via the Plane of Sky
class-unlock quests, using the dump files EverQuest-style clients write out
via `/outputfile achievements` and `/outputfile inventory`. It also
cross-references what's sitting in your bags/bank/keyring against those
quests, so after a farming run you can tell at a glance what to keep and
what's safe to sell or destroy.

## Why this works without a fragile, hand-maintained quest database

The game's own achievement export already lists every item required for
each class's "Primary Class Unlock" achievement, and flags each one
complete/incomplete itself. This app reads that directly — it does not
guess or hardcode quest requirements, so it can't drift out of sync with
whatever the server's current quest data actually is. A bundled
`data/plane_of_sky_item_sources.json` file adds optional "how do I get this"
hints (NPC + turn-in items, sourced from eqlwiki.com) purely as a
convenience layer on top of that authoritative data — if it's ever missing
or stale, the core tracker still works correctly, it just won't have a tip
for that item, and the sell/destroy check below won't cover it either.

## In-game setup (do this first, every time you want fresh data)

```
/outputfile achievements
/outputfile inventory
```

This writes `<Character>_<Server>-Achievements.txt` and
`<Character>_<Server>-Inventory.txt` directly into the game's install
directory — the same folder `eqgame.exe` (or `LaunchPad.exe`) runs from, NOT
a "My Documents" folder. On a Wine/Proton setup that's typically something
like:

```
~/Games/<PrefixName>/drive_c/users/Public/Daybreak Game Company/Installed Games/<GameName>/
```

Re-run both commands each time you want the tracker to see your latest
progress — the app just reads whatever those two files currently say, it
doesn't watch for changes.

## Run it

No install step needed — the app has zero third-party dependencies, so it
runs straight out of the repo:

```
./run.sh
```

That's the single command: it opens the GUI, which auto-detects your dump
folder (see "How the folder is found" below) or lets you browse to it with
"Choose folder...", and remembers your choice for next time. `run.sh`
always opens the GUI; for terminal-mode flags like `--list-chars` or
`--dir`, call `python3 -m eqskytracker` directly (see below).

If you'd rather not use the script, the equivalent is:

```
python3 -m eqskytracker --gui
```

For a one-off terminal report instead of the GUI:

```
python3 -m eqskytracker --dir "/path/to/your/EverQuest folder"
```

Requires Python 3.10+. The GUI needs Tk support (ships with the standard
python.org installers on Windows/macOS; on Linux you may need a
`python3-tk` package from your distro — if it's missing, `run.sh` will fail
with an `ImportError: No module named '_tkinter'`, which is your cue to
install it).

### Optional: install it as a regular command

```
pip install -e .
```

gives you a plain `eqskytracker` command instead of `python3 -m
eqskytracker` / `./run.sh`. Skip this if your system Python is
"externally managed" and refuses global `pip install`s (common on recent
Debian/Ubuntu) — `run.sh` works fine without it, since there's nothing to
install.

## How the folder is found

In order of priority:

1. `--dir` on the command line, if given.
2. The folder you last picked via the GUI's "Choose folder..." button or a
   previous `--dir` run — remembered in a `config.json` under:
   - Linux: `$XDG_CONFIG_HOME/eqskytracker` (defaults to `~/.config/eqskytracker`)
   - macOS: `~/Library/Application Support/eqskytracker`
   - Windows: `%APPDATA%\eqskytracker`
3. The `EQSKYTRACKER_DIR` environment variable, if set.
4. A handful of common Wine/Proton and native install locations (e.g.
   `~/Games/*/drive_c/users/Public/Daybreak Game Company/Installed
   Games/*`, `~/.wine/...`, `~/Documents/EverQuest`). These are bounded,
   non-recursive guesses — they won't find an install in a nonstandard
   location, so use `--dir` or the GUI's folder picker for anything unusual.
5. The current working directory, as a last resort (this is why `run.sh`
   still works if you `cd` into the repo and run it directly).

Use `--list-chars` to see which character dumps were found in the resolved
folder, and `--char <name>` to pick one when a folder has dumps for more
than one character.

## What it shows

- Per class: unlocked or not (mirrors the game's own achievement flag, not
  a heuristic), and which of the required items you've obtained. Add
  `--all` to expand the item checklist for classes that are already fully
  unlocked too (unlocked classes are always listed, just collapsed by
  default).
- For items you still need: a "how to get it" hint when available (turn-in
  NPC + required components), and a flag if the item is currently sitting
  in your bags/bank/keyring.
- **Farmed items check**: everything in your bags/bank/keyring that
  matches a known Plane of Sky turn-in component is listed as either
  "keep — still needed for `<reward>`" or "safe to sell/destroy" (its
  linked reward is already unlocked, so the component is just clutter).
  This only covers items the bundled hint data recognizes as PoS turn-ins —
  it says nothing about the rest of your inventory.

## Development

```
python3 -m unittest discover -s tests
```

Fixtures under `tests/fixtures/` are small, hand-written files that mirror
the real dump format (tab-delimited, CRLF line endings, the inventory
file's two-section item/keyring layout) — they're not real character data.
