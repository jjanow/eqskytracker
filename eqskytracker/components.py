"""Parses the turn-in "component" items named in a hint's `how_to_obtain`
prose (e.g. "Sphinx Claw", "Wind Rune Izah") -- the raw drops a player farms
in Plane of Sky and turns in to an NPC for a class-unlock reward, as opposed
to the reward item itself.

There's no structured component list in the bundled hint data, only this
one consistently-formatted sentence shape ("Turn in X, Y plus Wind Rune Z to
<NPC> to complete '<achievement>' (reward: <R>)."), so this is a regex parse
of that specific shape rather than a general free-text parser. If a hint's
text doesn't match the shape, parsing simply yields no components for it --
callers should treat that as "unknown," not as an error.
"""
from __future__ import annotations

import re

_HOW_TO_OBTAIN_RE = re.compile(r"^Turn in (.+) to .+ to complete '.+' \(reward: .+\)\.$")
_TAG_SUFFIX_RE = re.compile(r"\s*\([^()]*\)\s*$")


def parse_components(how_to_obtain: str) -> list[str]:
    """Extract turn-in component item names (island-tag parentheticals
    stripped) from a hint's how_to_obtain text. Returns [] if the text
    doesn't match the expected sentence shape."""
    m = _HOW_TO_OBTAIN_RE.match(how_to_obtain)
    if not m:
        return []
    items_part = m.group(1)
    if " plus " in items_part:
        listed, wind_rune = items_part.rsplit(" plus ", 1)
    else:
        listed, wind_rune = items_part, None
    names = [_TAG_SUFFIX_RE.sub("", n).strip() for n in listed.split(", ")]
    if wind_rune:
        names.append(wind_rune.strip())
    return [n for n in names if n]
