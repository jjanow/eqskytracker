"""Parser for EverQuest-style "/outputfile achievements" dumps.

File format (tab-delimited, CRLF line endings):

    Untapped Potential: Classes
    C	Primary Class Unlock - Bard
    C		Obtain Mask of Song.
    C		Obtain Mantle of the Songweaver.
    I		This achievement can be bypassed using a Primary Class Unlock Token.

- A line with no leading "C"/"I" column and no tabs is a category header.
- A line "C\\t<name>" or "I\\t<name>" is a top-level achievement, with C/I
  reflecting whether the game itself considers it complete.
- A line "C\\t\\t<text>" or "I\\t\\t<text>" is a sub-requirement of the most
  recently seen achievement.
"""
from __future__ import annotations

from dataclasses import dataclass, field
from pathlib import Path
import re

from .dumpfile import read_dump_lines

CLASS_UNLOCK_RE = re.compile(r"^Primary Class Unlock - (.+)$")
OBTAIN_RE = re.compile(r"^Obtain (.+?)\.?$")


@dataclass
class Requirement:
    text: str
    complete: bool

    @property
    def item_name(self) -> str | None:
        """The item name if this requirement is an "Obtain X." line, else None."""
        m = OBTAIN_RE.match(self.text)
        return m.group(1) if m else None


@dataclass
class Achievement:
    name: str
    complete: bool
    category: str
    requirements: list[Requirement] = field(default_factory=list)

    @property
    def item_requirements(self) -> list[Requirement]:
        return [r for r in self.requirements if r.item_name is not None]


def parse_achievements(path: str | Path) -> list[Achievement]:
    achievements: list[Achievement] = []
    category = ""
    current: Achievement | None = None

    for line in read_dump_lines(path):
        if not line:
            continue
        parts = line.split("\t")
        status_flag = parts[0]
        if status_flag not in ("C", "I"):
            category = line.strip()
            current = None
            continue
        complete = status_flag == "C"
        if len(parts) == 2:
            current = Achievement(name=parts[1].strip(), complete=complete, category=category)
            achievements.append(current)
        elif len(parts) >= 3 and current is not None:
            text = parts[-1].strip()
            if text:
                current.requirements.append(Requirement(text=text, complete=complete))
    return achievements


@dataclass
class ClassUnlock:
    class_name: str
    unlocked: bool
    items: list[Requirement]

    @property
    def obtained_count(self) -> int:
        return sum(1 for i in self.items if i.complete)

    @property
    def total_count(self) -> int:
        return len(self.items)


def class_unlocks(achievements: list[Achievement]) -> list[ClassUnlock]:
    """Extract the 'Primary Class Unlock - X' achievements as ClassUnlock records."""
    out = []
    for ach in achievements:
        m = CLASS_UNLOCK_RE.match(ach.name)
        if not m:
            continue
        out.append(ClassUnlock(
            class_name=m.group(1),
            unlocked=ach.complete,
            items=ach.item_requirements,
        ))
    return out
