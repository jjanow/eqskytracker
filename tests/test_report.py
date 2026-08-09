import unittest
from pathlib import Path

from eqskytracker.report import build_report

FIXTURES = Path(__file__).parent / "fixtures"
ACHIEVEMENTS = FIXTURES / "sample-Achievements.txt"
INVENTORY = FIXTURES / "sample-Inventory.txt"
HINTS = FIXTURES / "sample-hints.json"


class TestReport(unittest.TestCase):
    def test_character_name_derived_from_filename(self):
        report = build_report(ACHIEVEMENTS)
        self.assertEqual(report.character_name, "sample")

    def test_unlocked_count(self):
        report = build_report(ACHIEVEMENTS)
        self.assertEqual(report.unlocked_count, 1)
        self.assertEqual(report.total_classes, 2)

    def test_works_without_inventory(self):
        report = build_report(ACHIEVEMENTS)
        warrior = next(c for c in report.classes if c.class_name == "TestWarrior")
        self.assertTrue(all(not i.in_inventory for i in warrior.items))

    def test_in_inventory_cross_reference(self):
        report = build_report(ACHIEVEMENTS, INVENTORY)
        warrior = next(c for c in report.classes if c.class_name == "TestWarrior")
        dagas = next(i for i in warrior.items if i.name == "Dagas")
        self.assertFalse(dagas.in_inventory)  # not present anywhere in the fixture inventory
        belt = next(i for i in warrior.items if i.name == "Belt of the Four Winds")
        self.assertTrue(belt.in_inventory)

    def test_compound_item_name_matches_either_half(self):
        report = build_report(ACHIEVEMENTS, INVENTORY)
        warrior = next(c for c in report.classes if c.class_name == "TestWarrior")
        compound = next(i for i in warrior.items if i.name == "Fangol and Spirit Blade")
        self.assertTrue(compound.in_inventory)  # "Fangol" half is present in bags

    def test_hints_are_attached_when_supplied(self):
        report = build_report(ACHIEVEMENTS, INVENTORY, hints_path=HINTS)
        warrior = next(c for c in report.classes if c.class_name == "TestWarrior")
        compound = next(i for i in warrior.items if i.name == "Fangol and Spirit Blade")
        self.assertIsNotNone(compound.hint)
        self.assertEqual(compound.hint.npc, "Test NPC")

    def test_no_hint_for_unlisted_item(self):
        report = build_report(ACHIEVEMENTS, INVENTORY, hints_path=HINTS)
        warrior = next(c for c in report.classes if c.class_name == "TestWarrior")
        dagas = next(i for i in warrior.items if i.name == "Dagas")
        self.assertIsNone(dagas.hint)

    def test_farmed_component_flagged_needed_when_reward_incomplete(self):
        # "Fangol and Spirit Blade" is still incomplete in the fixture
        # achievements, and "Djinni War Blade" (its turn-in component) is
        # sitting in bags.
        report = build_report(ACHIEVEMENTS, INVENTORY, hints_path=HINTS)
        blade = next(f for f in report.farmed_items if f.name == "Djinni War Blade")
        self.assertFalse(blade.safe_to_sell)
        self.assertEqual(blade.needed_for, ["Fangol and Spirit Blade"])

    def test_farmed_component_flagged_safe_to_sell_when_reward_complete(self):
        # Belt of the Four Winds is already complete, so its leftover
        # turn-in component ("Fine Belt Buckle") is just clutter.
        report = build_report(ACHIEVEMENTS, INVENTORY, hints_path=HINTS)
        buckle = next(f for f in report.farmed_items if f.name == "Fine Belt Buckle")
        self.assertTrue(buckle.safe_to_sell)
        self.assertEqual(buckle.needed_for, [])

    def test_no_farmed_items_without_inventory(self):
        report = build_report(ACHIEVEMENTS, hints_path=HINTS)
        self.assertEqual(report.farmed_items, [])


if __name__ == "__main__":
    unittest.main()
