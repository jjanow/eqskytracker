# eqskytracker

Tracks your progress toward unlocking all classes via the Plane of Sky
class-unlock quests, using the dump files EverQuest-style clients write out
via `/outputfile achievements` and `/outputfile inventory`. It also
cross-references what's sitting in your bags/bank/keyring against those
quests, so after a farming run you can tell at a glance what to keep and
what's safe to sell or destroy.

Cross-platform .NET (C#) app: a desktop GUI (Avalonia UI) plus a terminal
CLI, both built on a shared core library. Runs on Windows, Linux, and macOS.

## Why this works without a fragile, hand-maintained quest database

The game's own achievement export already lists every item required for
each class's "Primary Class Unlock" achievement, and flags each one
complete/incomplete itself. This app reads that directly — it does not
guess or hardcode quest requirements, so it can't drift out of sync with
whatever the server's current quest data actually is. A bundled
`src/EqSkyTracker.Core/data/plane_of_sky_item_sources.json` file adds
optional "how do I get this" hints (NPC + turn-in items, sourced from
eqlwiki.com) purely as a convenience layer on top of that authoritative
data — if it's ever missing or stale, the core tracker still works
correctly, it just won't have a tip for that item, and the sell/destroy
check below won't cover it either.

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

There are two ways to run the app, depending on whether you want a
zero-install executable or you're working from a source checkout.

### Zero-install executable (recommended for just using the app)

Run `./publish.sh` (Linux/macOS) or `publish.bat` (Windows) once from a
checked-out copy of this repo — it needs the .NET SDK to *build* the
executables, but the result needs nothing installed to *run*: no .NET
runtime, no Python, nothing. It produces self-contained folders under
`publish/<runtime-id>/`, one per platform (`win-x64`, `linux-x64`,
`osx-arm64` by default):

```
publish/win-x64/EqSkyTracker.Gui/EqSkyTracker.Gui.exe
publish/win-x64/EqSkyTracker.Cli/EqSkyTracker.Cli.exe
```

Copy the whole `EqSkyTracker.Gui` (or `EqSkyTracker.Cli`) folder to the
target machine — the data file it depends on ships alongside the
executable — and double-click (Windows/macOS) or run it directly
(Linux). On Windows this is genuinely "download folder, double-click
`.exe`, done" — no installer, no runtime prerequisite.

### From a source checkout (needs the .NET SDK)

Requires the [.NET SDK](https://dotnet.microsoft.com/download) (10.0 or
later) installed. Pick the launcher for your platform:

- **Linux/macOS (terminal):** `./run.sh`
- **macOS (double-click in Finder):** `run.command`
- **Windows (double-click in Explorer, or from a terminal):** `run.bat`

These always open the GUI, auto-detecting your dump folder (see "How the
folder is found" below) or letting you browse to it with "Choose
folder...", and remembering your choice for next time.

For a one-off terminal report instead of the GUI:

```
dotnet run --project src/EqSkyTracker.Cli -- --dir "/path/to/your/EverQuest folder"
```

or, from a published build:

```
publish/<rid>/EqSkyTracker.Cli/EqSkyTracker.Cli --dir "/path/to/your/EverQuest folder"
```

## How the folder is found

In order of priority:

1. `--dir` on the command line, if given.
2. The folder you last picked via the GUI's "Choose folder..." button or a
   previous `--dir` run — remembered in a `config.json` next to the
   executable, so the app stays self-contained and portable rather than
   writing into your home/profile directory. That same file also remembers
   the GUI window's size and position between runs.
3. The `EQSKYTRACKER_DIR` environment variable, if set.
4. A handful of common Wine/Proton and native install locations (e.g.
   `~/Games/*/drive_c/users/Public/Daybreak Game Company/Installed
   Games/*`, `~/.wine/...`, `~/Documents/EverQuest`). These are bounded,
   non-recursive guesses — they won't find an install in a nonstandard
   location, so use `--dir` or the GUI's folder picker for anything unusual.
5. The current working directory, as a last resort.

Use `--list-chars` to see which character dumps were found in the resolved
folder, and `--char <name>` to pick one when a folder has dumps for more
than one character.

## What it shows

- Per class: unlocked or not (mirrors the game's own achievement flag, not
  a heuristic), and which of the required items you've obtained. Add
  `--all` to expand the item checklist for classes that are already fully
  unlocked too (unlocked classes are always listed, just collapsed by
  default in the CLI; the GUI always shows both).
- For items you still need: a "how to get it" hint when available (turn-in
  NPC + required components), a flag if the item is currently sitting
  in your bags/bank/keyring, and — when a hint is available and an
  inventory dump was supplied — a turn-in readiness check: how many of the
  named components you already have (e.g. "1/2 components in bags"), or
  "✓ ready to turn in" once you have all of them. A turn-in that also needs
  a Wind Rune is flagged separately ("components ready, Wind Rune
  unverified") since Wind Runes live in an alternate-currency window and
  never show up in an inventory dump.
- **Farmed items check**: everything in your bags/bank/keyring that
  matches a known Plane of Sky turn-in component is listed as either
  "keep — still needed for `<reward>`" or "safe to sell/destroy" (its
  linked reward is already unlocked, so the component is just clutter).
  This only covers items the bundled hint data recognizes as PoS turn-ins —
  it says nothing about the rest of your inventory.

## Project layout

- `src/EqSkyTracker.Core` — parsers (achievements/inventory dump files),
  report building, folder/character discovery, config persistence. No UI
  dependencies.
- `src/EqSkyTracker.Cli` — terminal report / `--list-chars` / `--dir`.
- `src/EqSkyTracker.Gui` — the Avalonia desktop GUI (Windows/Linux/macOS).
- `tests/EqSkyTracker.Tests` — xUnit tests for the core library.

## Development

```
dotnet test
```

Fixtures under `tests/EqSkyTracker.Tests/fixtures/` are small, hand-written
files that mirror the real dump format (tab-delimited, CRLF line endings,
the inventory file's two-section item/keyring layout) — they're not real
character data.
