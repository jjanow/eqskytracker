import unittest
from pathlib import Path

from eqskytracker.achievements import parse_achievements, class_unlocks

FIXTURE = Path(__file__).parent / "fixtures" / "sample-Achievements.txt"


class TestAchievements(unittest.TestCase):
    def setUp(self):
        self.achievements = parse_achievements(FIXTURE)
        self.unlocks = class_unlocks(self.achievements)

    def test_categories_are_not_achievements(self):
        names = [a.name for a in self.achievements]
        self.assertNotIn("Untapped Potential: Classes", names)
        self.assertNotIn("General: Keys", names)

    def test_finds_both_class_unlocks(self):
        self.assertEqual({u.class_name for u in self.unlocks}, {"TestBard", "TestWarrior"})

    def test_fully_complete_class_is_unlocked(self):
        bard = next(u for u in self.unlocks if u.class_name == "TestBard")
        self.assertTrue(bard.unlocked)
        self.assertEqual(bard.obtained_count, 2)
        self.assertEqual(bard.total_count, 2)

    def test_partial_class_is_not_unlocked(self):
        warrior = next(u for u in self.unlocks if u.class_name == "TestWarrior")
        self.assertFalse(warrior.unlocked)
        self.assertEqual(warrior.obtained_count, 1)
        self.assertEqual(warrior.total_count, 3)

    def test_item_names_strip_obtain_prefix_and_period(self):
        warrior = next(u for u in self.unlocks if u.class_name == "TestWarrior")
        names = {i.item_name for i in warrior.items}
        self.assertEqual(names, {"Belt of the Four Winds", "Dagas", "Fangol and Spirit Blade"})

    def test_meta_lines_are_not_item_requirements(self):
        warrior = next(u for u in self.unlocks if u.class_name == "TestWarrior")
        texts = {r.text for r in warrior.items}
        self.assertNotIn("This achievement can be bypassed using a Primary Class Unlock Token.", texts)

    def test_non_class_achievements_are_ignored_by_class_unlocks(self):
        self.assertFalse(any(u.class_name == "Islands of Sky Keys" for u in self.unlocks))


if __name__ == "__main__":
    unittest.main()
