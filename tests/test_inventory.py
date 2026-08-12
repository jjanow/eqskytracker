import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

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


class TestSectionBreakEdgeCases(unittest.TestCase):
    # Regression tests for the switch from line-by-line file iteration to
    # read_dump_text(...).split("\n") -- that rewrite (for cp1252 fallback
    # decoding, see test_dumpfile.py) changes how a trailing newline turns
    # into a trailing "" entry in `lines`, which is exactly what the
    # section-break scan keys off of.

    def _parse(self, raw_bytes: bytes):
        with TemporaryDirectory() as tmp:
            p = Path(tmp) / "dump.txt"
            p.write_bytes(raw_bytes)
            return parse_inventory(p)

    def test_keyring_parses_with_trailing_newline(self):
        raw = (
            b"Location\tName\tID\tCount\tSlots\r\n"
            b"Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"
            b"\r\n"
            b"KeyRing\tName\tID\r\n"
            b"Augmentation\tMask of Song\t1408\r\n"
        )
        inv = self._parse(raw)
        self.assertEqual(len(inv.items), 1)
        self.assertEqual(len(inv.keyring), 1)
        self.assertEqual(inv.keyring[0].name, "Mask of Song")

    def test_keyring_parses_without_trailing_newline(self):
        raw = (
            b"Location\tName\tID\tCount\tSlots\r\n"
            b"Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"
            b"\r\n"
            b"KeyRing\tName\tID\r\n"
            b"Augmentation\tMask of Song\t1408"  # no trailing newline at EOF
        )
        inv = self._parse(raw)
        self.assertEqual(len(inv.keyring), 1)
        self.assertEqual(inv.keyring[0].name, "Mask of Song")

    def test_keyring_parses_with_extra_blank_line_before_header(self):
        raw = (
            b"Location\tName\tID\tCount\tSlots\r\n"
            b"Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"
            b"\r\n"
            b"\r\n"
            b"KeyRing\tName\tID\r\n"
            b"Augmentation\tMask of Song\t1408\r\n"
        )
        inv = self._parse(raw)
        self.assertEqual(len(inv.keyring), 1)

    def test_no_keyring_section_at_all(self):
        raw = (
            b"Location\tName\tID\tCount\tSlots\r\n"
            b"Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"
        )
        inv = self._parse(raw)
        self.assertEqual(len(inv.items), 1)
        self.assertEqual(inv.keyring, [])


if __name__ == "__main__":
    unittest.main()
