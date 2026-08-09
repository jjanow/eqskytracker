import unittest
from pathlib import Path

from eqskytracker.inventory import normalize_item_name, parse_inventory

FIXTURE = Path(__file__).parent / "fixtures" / "sample-Inventory.txt"


class TestInventory(unittest.TestCase):
    def setUp(self):
        self.inv = parse_inventory(FIXTURE)

    def test_skips_empty_slots(self):
        self.assertTrue(all(i.name != "Empty" for i in self.inv.items))

    def test_parses_item_slots(self):
        self.assertEqual(len(self.inv.items), 6)  # Empty slot excluded

    def test_parses_keyring_separately(self):
        self.assertEqual(len(self.inv.keyring), 1)
        self.assertEqual(self.inv.keyring[0].name, "Mask of Song")

    def test_has_item_matches_bag_item(self):
        self.assertTrue(self.inv.has_item("Belt of the Four Winds"))

    def test_has_item_matches_keyring_item(self):
        self.assertTrue(self.inv.has_item("Mask of Song"))

    def test_has_item_false_for_missing(self):
        self.assertFalse(self.inv.has_item("Nonexistent Trinket"))

    def test_normalize_strips_power_tier_suffix(self):
        self.assertEqual(normalize_item_name("Spiroc Wingblade +2"), "Spiroc Wingblade")

    def test_normalize_strips_exaltation_suffix(self):
        self.assertEqual(normalize_item_name("Spiroc Wingblade (Exaltation)"), "Spiroc Wingblade")

    def test_power_tier_and_exaltation_copies_match_same_name(self):
        matches = self.inv.find_by_name("Spiroc Wingblade")
        self.assertEqual(len(matches), 2)
        ids = {m.item_id for m in matches}
        self.assertEqual(ids, {20679})


if __name__ == "__main__":
    unittest.main()
