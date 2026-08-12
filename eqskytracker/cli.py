from __future__ import annotations

import argparse
import sys
from pathlib import Path

from .discovery import candidate_dirs, find_all_characters, save_last_dir
from .report import build_report, CharacterReport


def print_report(report: CharacterReport, show_complete: bool = False) -> None:
    print(f"\n{report.character_name} -- Plane of Sky class unlocks: "
          f"{report.unlocked_count}/{report.total_classes}\n")
    for cls in sorted(report.classes, key=lambda c: (c.unlocked, c.class_name)):
        if cls.unlocked and not show_complete:
            print(f"  [DONE] {cls.class_name} ({cls.obtained_count}/{cls.total_count})")
            continue
        mark = "DONE" if cls.unlocked else "    "
        print(f"  [{mark}] {cls.class_name} ({cls.obtained_count}/{cls.total_count})")
        for item in cls.items:
            box = "x" if item.complete else " "
            line = f"        [{box}] {item.name}"
            if not item.complete:
                if item.in_inventory:
                    line += "  (already in your bags/bank!)"
                if item.hint and item.hint.found and item.hint.how_to_obtain:
                    line += f"\n            -> {item.hint.how_to_obtain}"
            print(line)
    print()

    if report.farmed_items:
        needed = [f for f in report.farmed_items if not f.safe_to_sell]
        sellable = [f for f in report.farmed_items if f.safe_to_sell]
        print("Plane of Sky turn-in components currently in your bags/bank/keyring:")
        if needed:
            print("  Still needed -- keep these:")
            for f in needed:
                where = ", ".join(f.locations)
                print(f"    [keep] {f.name} x{f.count} ({where}) -- needed for: {', '.join(f.needed_for)}")
        if sellable:
            print("  Not needed for anything incomplete -- safe to sell/destroy:")
            for f in sellable:
                where = ", ".join(f.locations)
                print(f"    [sell] {f.name} x{f.count} ({where})")
        print()


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Track Plane of Sky class-unlock progress.")
    parser.add_argument("--dir", help="Folder containing <Character>-Achievements.txt / -Inventory.txt dumps")
    parser.add_argument("--char", help="Character name (e.g. Tholi_rivervale). "
                                        "Required if multiple characters' dumps are in the same folder")
    parser.add_argument("--all", action="store_true",
                         help="Expand item checklists for fully-unlocked classes too (they're always listed)")
    parser.add_argument("--gui", action="store_true", help="Launch the graphical interface")
    parser.add_argument("--list-chars", action="store_true", help="List discovered characters and exit")
    args = parser.parse_args(argv)

    if args.dir and not Path(args.dir).is_dir():
        print(f"--dir '{args.dir}' is not a directory.", file=sys.stderr)
        return 1

    if args.gui:
        from .gui import run_gui
        return run_gui(initial_dir=args.dir)

    dirs = [Path(args.dir)] if args.dir else candidate_dirs()
    characters = find_all_characters(dirs)

    if args.list_chars:
        for c in characters:
            print(c.name, "-", "achievements" if c.achievements_path else "no achievements file",
                  "+", "inventory" if c.inventory_path else "no inventory file")
        return 0

    if not characters:
        print("No character dump files found. Point me at the folder with --dir, "
              "or run '/outputfile achievements' and '/outputfile inventory' in-game first.",
              file=sys.stderr)
        return 1

    if args.char:
        matches = [c for c in characters if c.name == args.char]
        if not matches:
            print(f"No dumps found for character '{args.char}'. Known: "
                  f"{', '.join(c.name for c in characters)}", file=sys.stderr)
            return 1
        target = matches[0]
    elif len(characters) == 1:
        target = characters[0]
    else:
        print("Multiple characters found, pick one with --char:")
        for c in characters:
            print(" -", c.name)
        return 1

    if not target.achievements_path:
        print(f"{target.name} has an inventory dump but no achievements dump -- "
              f"run '/outputfile achievements' in-game and try again.", file=sys.stderr)
        return 1

    if args.dir:
        save_last_dir(args.dir)

    report = build_report(target.achievements_path, target.inventory_path)
    print_report(report, show_complete=args.all)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
