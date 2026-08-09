"""Parser for EverQuest-style "/outputfile inventory" dumps.

The file has two tab-delimited sections separated by a blank line:

    Location	Name	ID	Count	Slots
    Any Slot	Eye of Innoruuk +5	20656	1	10
    ...
    <blank line>
    KeyRing	Name	ID
    Augmentation	Nightshade Wreath (Exaltation)	1408
    ...

Item names carry cosmetic suffixes (" +N" power-tiers, " (Exaltation)"
augment-slot copies) that must be stripped before matching against quest/
achievement item names, since "Spiroc Wingblade +2" and "Spiroc Wingblade
(Exaltation)" both refer to item ID 20679.
"""
from __future__ import annotations

from dataclasses import dataclass
from pathlib import Path
import re

_SUFFIX_RE = re.compile(r"\s*(\+\d+|\(Exaltation\))\s*$")


def normalize_item_name(name: str) -> str:
    """Strip trailing ' +N' / ' (Exaltation)' decorations for name matching."""
    prev = None
    while prev != name:
        prev = name
        name = _SUFFIX_RE.sub("", name).strip()
    return name


@dataclass
class InventoryItem:
    location: str
    name: str
    item_id: int
    count: int
    slots: int

    @property
    def normalized_name(self) -> str:
        return normalize_item_name(self.name)

    @property
    def is_exaltation_copy(self) -> bool:
        return "(Exaltation)" in self.name


@dataclass
class KeyringItem:
    category: str
    name: str
    item_id: int

    @property
    def normalized_name(self) -> str:
        return normalize_item_name(self.name)


@dataclass
class Inventory:
    items: list[InventoryItem]
    keyring: list[KeyringItem]

    def find_by_name(self, name: str) -> list[InventoryItem]:
        target = normalize_item_name(name).casefold()
        return [i for i in self.items if i.normalized_name.casefold() == target]

    def find_in_keyring(self, name: str) -> list[KeyringItem]:
        target = normalize_item_name(name).casefold()
        return [k for k in self.keyring if k.normalized_name.casefold() == target]

    def has_item(self, name: str) -> bool:
        return bool(self.find_by_name(name) or self.find_in_keyring(name))


def parse_inventory(path: str | Path) -> Inventory:
    items: list[InventoryItem] = []
    keyring: list[KeyringItem] = []

    with open(path, encoding="utf-8-sig", newline="") as f:
        lines = [raw.rstrip("\r\n") for raw in f]

    # Section 1: item slots, up to the first blank line.
    section_break = len(lines)
    for idx, line in enumerate(lines):
        if line == "":
            section_break = idx
            break

    for line in lines[:section_break]:
        parts = line.split("\t")
        if parts[0] == "Location":
            continue  # header row
        if len(parts) < 5:
            continue
        location, name, item_id, count, slots = parts[:5]
        if name == "Empty":
            continue
        try:
            items.append(InventoryItem(
                location=location,
                name=name,
                item_id=int(item_id),
                count=int(count),
                slots=int(slots),
            ))
        except ValueError:
            continue

    # Section 2: keyring, after the blank line.
    for line in lines[section_break + 1:]:
        if line == "":
            continue
        parts = line.split("\t")
        if parts[0] == "KeyRing":
            continue  # header row
        if len(parts) < 3:
            continue
        category, name, item_id = parts[:3]
        try:
            keyring.append(KeyringItem(category=category, name=name, item_id=int(item_id)))
        except ValueError:
            continue

    return Inventory(items=items, keyring=keyring)
