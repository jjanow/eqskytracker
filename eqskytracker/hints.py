"""Optional 'how do I get this item' hints, loaded from a bundled JSON file.

This is pure decoration on top of the achievement-derived completion data --
if the file is missing or a given item isn't in it, the app still works
correctly, it just won't have a tip for that item.
"""
from __future__ import annotations

from dataclasses import dataclass
import json
from pathlib import Path

DATA_FILE = Path(__file__).resolve().parent / "data" / "plane_of_sky_item_sources.json"


@dataclass
class ItemHint:
    npc: str | None
    zone: str | None
    how_to_obtain: str | None
    found: bool


def load_item_hints(path: Path | None = None) -> dict[str, ItemHint]:
    path = path or DATA_FILE
    if not path.exists():
        return {}
    try:
        raw = json.loads(path.read_text(encoding="utf-8"))
    except (OSError, ValueError):
        return {}

    out: dict[str, ItemHint] = {}
    for name, info in raw.get("item_sources", {}).items():
        if not isinstance(info, dict):
            continue
        # `info["class"]` (wiki's class label, sometimes spelled differently
        # than the achievement export, e.g. "Shadow Knight" vs "Shadowknight")
        # is intentionally not surfaced here -- ItemStatus already carries the
        # achievement-derived class grouping, so keeping this one avoids two
        # sources of truth for the same fact.
        out[name.casefold()] = ItemHint(
            npc=info.get("npc"),
            zone=info.get("zone_or_island"),
            how_to_obtain=info.get("how_to_obtain"),
            found=bool(info.get("found", False)),
        )
    return out
