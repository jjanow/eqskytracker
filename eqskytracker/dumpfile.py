"""Shared decoding for EverQuest-style /outputfile dumps.

These files are written by the game client, which is not guaranteed to
encode non-ASCII bytes (curly quotes, accented letters in item/NPC names)
as UTF-8 -- older or non-English-locale Windows clients commonly emit the
system codepage (e.g. cp1252) instead. Decoding must never blow up the
parse over a single unexpected byte, so this tries UTF-8 first (the
correct case) and falls back to progressively more permissive decodings.
"""
from __future__ import annotations

from pathlib import Path


def read_dump_text(path: str | Path) -> str:
    raw = Path(path).read_bytes()
    for encoding in ("utf-8-sig", "cp1252"):
        try:
            return raw.decode(encoding)
        except UnicodeDecodeError:
            continue
    return raw.decode("latin-1", errors="replace")


def read_dump_lines(path: str | Path) -> list[str]:
    """Lines with trailing \\r\\n / \\n stripped, without splitting on the
    wider set of line boundaries str.splitlines() recognizes (some of which
    -- \\x0c, \\x1c-\\x1e -- could otherwise appear in a cp1252-decoded
    line and split it unexpectedly)."""
    return [line.rstrip("\r") for line in read_dump_text(path).split("\n")]
