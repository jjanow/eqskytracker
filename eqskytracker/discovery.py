"""Locate character dump files and remember the last-used folder.

EQ-style clients write "/outputfile achievements" and "/outputfile inventory"
as `<Character>_<Server>-Achievements.txt` / `-Inventory.txt` directly into
the game's install/working directory. There's no single blessed path across
Windows/macOS/Linux (and this app was built against a Wine install, so even
"typical" Windows guesses are speculative) -- so discovery is: look in a few
common places, but always let the user point at the real folder, and remember
whatever they picked.
"""
from __future__ import annotations

import json
import os
import platform
from dataclasses import dataclass
from pathlib import Path


@dataclass
class Character:
    name: str
    achievements_path: Path | None
    inventory_path: Path | None


def config_dir() -> Path:
    """The app's own install/package directory -- config.json lives alongside
    the source rather than under the user's home/profile, so the app is
    self-contained and portable (e.g. on a USB stick or a shared machine)."""
    return Path(__file__).resolve().parent


def _config_file() -> Path:
    return config_dir() / "config.json"


def _load_config() -> dict:
    try:
        return json.loads(_config_file().read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}


def _save_config_value(key: str, value: str) -> None:
    """Read-modify-write a single key so saving one setting (e.g. window
    geometry) never clobbers another (e.g. last_dir) already in the file.
    Best-effort: if the app's own directory isn't writable (e.g. installed
    system-wide into a read-only location), silently skip persisting rather
    than crashing -- remembering the folder/geometry is a convenience, not
    something the app depends on to function."""
    try:
        cdir = config_dir()
        cdir.mkdir(parents=True, exist_ok=True)
        data = _load_config()
        data[key] = value
        _config_file().write_text(json.dumps(data), encoding="utf-8")
    except OSError:
        pass


def load_last_dir() -> Path | None:
    value = _load_config().get("last_dir")
    return Path(value) if value else None


def save_last_dir(directory: str | Path) -> None:
    _save_config_value("last_dir", str(directory))


def load_window_geometry() -> str | None:
    """Tk geometry string (e.g. '900x600+120+80') from the previous run, if any."""
    value = _load_config().get("window_geometry")
    return value if isinstance(value, str) else None


def save_window_geometry(geometry: str) -> None:
    _save_config_value("window_geometry", geometry)


def _installed_games_dirs(installed_games: Path) -> list[Path]:
    """"Installed Games" holds one subfolder per installed title (e.g.
    "EverQuestLegends") -- the dump files live inside that subfolder, not in
    "Installed Games" itself. A single bounded iterdir(), never recursive."""
    out = [installed_games]
    try:
        out += [p for p in installed_games.iterdir() if p.is_dir()]
    except OSError:
        pass
    return out


def candidate_dirs() -> list[Path]:
    """Best-effort guesses, checked in addition to whatever the user supplies."""
    candidates: list[Path] = []
    last = load_last_dir()
    if last:
        candidates.append(last)

    env_dir = os.environ.get("EQSKYTRACKER_DIR")
    if env_dir:
        candidates.append(Path(env_dir))

    home = Path.home()
    system = platform.system()
    if system == "Windows":
        installed_games = Path(os.environ.get("PUBLIC", r"C:\Users\Public")) / "Daybreak Game Company" / "Installed Games"
        candidates += _installed_games_dirs(installed_games)
        candidates.append(home / "Documents" / "EverQuest")
    else:
        # A handful of shallow, fixed guesses for common Wine/Proton-prefix
        # layouts. Deliberately not a recursive glob() over $HOME -- on a
        # real machine that walks the whole home tree (game installs alone
        # can hold tens of thousands of asset files) and can take tens of
        # seconds, which would freeze the GUI before its first paint.
        #
        # Each Wine/Proton prefix is commonly its own directory rather than
        # one shared prefix (e.g. ~/Games/EQLegends, ~/Games/EQQuarm), so
        # check both a wine-roots parent directory itself *and* its
        # immediate subdirectories as candidate prefixes -- still just
        # bounded iterdir() calls, never a walk.
        wine_root_parents = [home / ".wine", home / "Games", home / ".local" / "share" / "wineprefixes"]
        prefixes: list[Path] = []
        for parent in wine_root_parents:
            prefixes.append(parent)
            try:
                prefixes += [p for p in parent.iterdir() if p.is_dir()]
            except OSError:
                pass

        for prefix in prefixes:
            installed_games = prefix / "drive_c" / "users" / "Public" / "Daybreak Game Company" / "Installed Games"
            candidates += _installed_games_dirs(installed_games)
        candidates.append(home / "Documents" / "EverQuest")

    candidates.append(Path.cwd())
    seen: set[Path] = set()
    unique = []
    for c in candidates:
        try:
            resolved = c.resolve()
        except OSError:
            continue
        if resolved not in seen and resolved.is_dir():
            seen.add(resolved)
            unique.append(resolved)
    return unique


def find_characters(directory: str | Path) -> list[Character]:
    """Scan a directory (non-recursive) for <name>-Achievements.txt / -Inventory.txt pairs.

    Guarded against OSError (e.g. PermissionError): a candidate directory
    can exist but not be listable -- notably on macOS, where TCC can deny
    directory-listing access to folders like ~/Documents until the user
    grants it -- and a permission error here must not crash the app on
    startup, it should just mean "no characters found in this folder"."""
    directory = Path(directory)
    names: dict[str, Character] = {}
    if not directory.is_dir():
        return []
    try:
        entries = list(directory.iterdir())
    except OSError:
        return []
    for path in entries:
        try:
            if not path.is_file():
                continue
        except OSError:
            continue
        if path.name.endswith("-Achievements.txt"):
            name = path.name[: -len("-Achievements.txt")]
            names.setdefault(name, Character(name=name, achievements_path=None, inventory_path=None))
            names[name].achievements_path = path
        elif path.name.endswith("-Inventory.txt"):
            name = path.name[: -len("-Inventory.txt")]
            names.setdefault(name, Character(name=name, achievements_path=None, inventory_path=None))
            names[name].inventory_path = path
    return sorted(names.values(), key=lambda c: c.name)


def find_all_characters(directories: list[Path]) -> list[Character]:
    seen: dict[str, Character] = {}
    for d in directories:
        for char in find_characters(d):
            seen.setdefault(char.name, char)
    return sorted(seen.values(), key=lambda c: c.name)
