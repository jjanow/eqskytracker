"""Ties achievements + inventory + optional hints into a report the UIs render."""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path

from .achievements import parse_achievements, class_unlocks
from .components import parse_components
from .inventory import parse_inventory, Inventory
from .hints import load_item_hints, ItemHint

ACHIEVEMENTS_SUFFIX = "-Achievements.txt"


@dataclass
class ItemStatus:
    name: str
    complete: bool
    in_inventory: bool
    hint: ItemHint | None


@dataclass
class FarmedItemStatus:
    """A Plane of Sky turn-in component currently sitting in the player's
    bags/bank/keyring, cross-referenced against every class-unlock reward
    known to need it."""
    name: str
    count: int
    locations: list[str]
    needed_for: list[str]  # reward item names still incomplete that need this; [] means safe to sell/destroy

    @property
    def safe_to_sell(self) -> bool:
        return not self.needed_for


@dataclass
class ClassReport:
    class_name: str
    unlocked: bool
    items: list[ItemStatus] = field(default_factory=list)

    @property
    def obtained_count(self) -> int:
        return sum(1 for i in self.items if i.complete)

    @property
    def total_count(self) -> int:
        return len(self.items)


@dataclass
class CharacterReport:
    character_name: str
    classes: list[ClassReport]
    farmed_items: list[FarmedItemStatus] = field(default_factory=list)

    @property
    def unlocked_count(self) -> int:
        return sum(1 for c in self.classes if c.unlocked)

    @property
    def total_classes(self) -> int:
        return len(self.classes)


def _has_any(inventory: Inventory, name: str) -> bool:
    """Some 'Obtain X' requirements name two items at once (e.g. 'Windhowl and
    Spirit Render'); treat those as satisfied if either half is present."""
    if inventory.has_item(name):
        return True
    if " and " in name:
        return any(inventory.has_item(part.strip()) for part in name.split(" and "))
    return False


def _character_name(achievements_path: Path) -> str:
    stem = achievements_path.name
    if stem.endswith(ACHIEVEMENTS_SUFFIX):
        return stem[: -len(ACHIEVEMENTS_SUFFIX)]
    return achievements_path.stem


def build_report(
    achievements_path: str | Path,
    inventory_path: str | Path | None = None,
    hints_path: str | Path | None = None,
) -> CharacterReport:
    achievements_path = Path(achievements_path)
    achievements = parse_achievements(achievements_path)
    unlocks = class_unlocks(achievements)

    inventory: Inventory | None = None
    if inventory_path and Path(inventory_path).exists():
        inventory = parse_inventory(inventory_path)

    hints = load_item_hints(Path(hints_path)) if hints_path else load_item_hints()

    classes = []
    for cu in unlocks:
        items = []
        for req in cu.items:
            name = req.item_name or req.text
            items.append(ItemStatus(
                name=name,
                complete=req.complete,
                in_inventory=_has_any(inventory, name) if inventory else False,
                hint=hints.get(name.casefold()),
            ))
        classes.append(ClassReport(class_name=cu.class_name, unlocked=cu.unlocked, items=items))

    farmed_items = _farmed_item_statuses(inventory, classes) if inventory else []

    return CharacterReport(
        character_name=_character_name(achievements_path),
        classes=classes,
        farmed_items=farmed_items,
    )


def _farmed_item_statuses(
    inventory: Inventory,
    classes: list[ClassReport],
) -> list[FarmedItemStatus]:
    """Cross-reference bag/bank/keyring contents against the turn-in
    components (parsed from hint text) needed by every still-incomplete
    class-unlock item, so farmed loot can be flagged as still-needed or
    safe to sell/destroy."""
    component_targets: dict[str, list[tuple[str, bool]]] = {}
    for cls in classes:
        for item in cls.items:
            if not item.hint or not item.hint.how_to_obtain:
                continue
            for component in parse_components(item.hint.how_to_obtain):
                component_targets.setdefault(component.casefold(), []).append((item.name, item.complete))

    grouped: dict[str, dict] = {}
    entries = [(i.normalized_name, i.count, i.location) for i in inventory.items]
    entries += [(k.normalized_name, 1, k.category) for k in inventory.keyring]
    for name, count, location in entries:
        key = name.casefold()
        if key not in component_targets:
            continue
        g = grouped.setdefault(key, {"name": name, "count": 0, "locations": set()})
        g["count"] += count
        g["locations"].add(location)

    statuses = []
    for key, g in grouped.items():
        needed_for = sorted({name for name, complete in component_targets[key] if not complete})
        statuses.append(FarmedItemStatus(
            name=g["name"],
            count=g["count"],
            locations=sorted(g["locations"]),
            needed_for=needed_for,
        ))
    statuses.sort(key=lambda s: (s.safe_to_sell, s.name))
    return statuses
