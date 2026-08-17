# eqskytracker

Tracks your progress toward unlocking all classes via the Plane of Sky
class-unlock quests, using the dump files EverQuest-style clients write out
via `/outputfile achievements` and `/outputfile inventory`. It also
cross-references what's sitting in your bags/bank/keyring against those
quests, so after a farming run you can tell at a glance what to keep and
what's safe to sell or destroy.

Cross-platform .NET (C#) app: a desktop GUI (Avalonia UI) plus a terminal
CLI, both built on a shared core library. Runs on Windows and Linux, with
packaged auto-updating releases for both; macOS works from a source build
but has no packaged release (see [Releasing](#releasing)).

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

There are three ways to run the app: download a release, self-publish a
zero-install build, or run from a source checkout.

### Download a release (Windows/Linux, recommended)

Grab the installer for your platform from the
[Releases page](https://github.com/jjanow/eqskytracker/releases) — a
Windows `Setup.exe` (or portable `.zip`), or a Linux `.AppImage`
(`chmod +x` it if it doesn't launch, then run it directly). No .NET
runtime needed either way.

Installed this way, the app checks for updates on startup and shows a
banner ("Restart Now" / "Later") when one's ready — see
[Releasing](#releasing) below for how new versions get built.
Packages aren't code-signed, so Windows SmartScreen will warn on first run
("More info" → "Run anyway" to proceed); see that section for why.

There's no macOS release — see [Releasing](#releasing) for why.
macOS users should use one of the two options below instead.

### Zero-install executable (self-published)

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
`.exe`, done" — no installer, no runtime prerequisite. Unlike a downloaded
release, a build produced this way does not check for or apply updates —
re-run `publish.sh`/`publish.bat` to get a newer version.

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
   previous `--dir` run — remembered in a per-user `config.json` (`%APPDATA%\EqSkyTracker`
   on Windows, `~/Library/Application Support/EqSkyTracker` on macOS,
   `~/.config/EqSkyTracker` on Linux), so it survives reinstalls and
   updates rather than living inside the app's own install directory. That
   same file also remembers the GUI window's size and position between
   runs.
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

## Releasing

Gui releases are built and published by
[`.github/workflows/release.yml`](.github/workflows/release.yml),
triggered by pushing a tag matching `v*`:

```
git tag v1.2.3
git push origin v1.2.3
```

That's the whole process — there's nothing to bump in the repo first. The
version number lives only in the tag; the workflow strips the leading `v`
and passes the rest straight through as the package version, so the tag
*is* the source of truth.

What the workflow does, per tag push:

1. Publishes `EqSkyTracker.Gui` self-contained for `linux-x64` and
   `win-x64` (`win-x64` is cross-compiled from the Linux runner — no
   Windows runner needed).
2. Packs each with [Velopack](https://velopack.io) (`vpk`): a `.AppImage`
   for Linux, a `Setup.exe` installer + portable `.zip` for Windows.
3. Uploads both to a single GitHub Release tagged with the pushed tag —
   Linux and Windows are separate Velopack *channels* in the same release,
   which is how the running app's update checker tells them apart.
4. Where possible, seeds the pack step with the previous release's
   packages first so Velopack can emit a small delta update alongside the
   full one, instead of every update being a full multi-MB download. The
   very first release (or if no prior release is found) is full-only —
   that's expected, not a failure.

`EqSkyTracker.Cli` is intentionally not part of this pipeline — it stays a
manual, unpackaged single-file build via `publish.sh`/`publish.bat`, with
no auto-update.

**Platforms**: Windows and Linux only. There is no macOS release, and
none is planned — it would need a paid Apple Developer account to notarize
builds, and modern macOS Gatekeeper specifically blocks unnotarized apps
from auto-updating (defeating the point of shipping this at all).

**Signing**: packages are unsigned. Windows SmartScreen will flag the
installer on first run; users click "More info" → "Run anyway" to
proceed. This is accepted for now rather than buying a code-signing
certificate — revisit if it becomes a real adoption blocker.

If a release run fails partway (e.g. the Windows upload step fails after
the Linux one already succeeded), it leaves a *draft* release behind
rather than a public one missing a platform — check the
[Releases page](https://github.com/jjanow/eqskytracker/releases) and
[Actions tab](https://github.com/jjanow/eqskytracker/actions) before
re-pushing the same tag; you'll likely need to delete the draft first.

## Development

```
dotnet test
```

Fixtures under `tests/EqSkyTracker.Tests/fixtures/` are small, hand-written
files that mirror the real dump format (tab-delimited, CRLF line endings,
the inventory file's two-section item/keyring layout) — they're not real
character data.
