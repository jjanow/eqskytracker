import unittest
from pathlib import Path
from tempfile import TemporaryDirectory

from eqskytracker.dumpfile import read_dump_lines


class TestReadDumpLines(unittest.TestCase):
    def test_utf8_file_decodes_normally(self):
        with TemporaryDirectory() as tmp:
            p = Path(tmp) / "dump.txt"
            p.write_text("C\tPrimary Class Unlock - Bard\r\nC\t\tObtain Mask of Song.\r\n",
                         encoding="utf-8")
            self.assertEqual(
                read_dump_lines(p),
                ["C\tPrimary Class Unlock - Bard", "C\t\tObtain Mask of Song.", ""],
            )

    def test_cp1252_bytes_fall_back_instead_of_raising(self):
        # Regression test: EQ-style clients don't guarantee UTF-8 output --
        # a curly apostrophe (cp1252 0x92) is invalid UTF-8 and used to raise
        # UnicodeDecodeError and crash the parse.
        with TemporaryDirectory() as tmp:
            p = Path(tmp) / "dump.txt"
            raw = "C\t\tObtain Ry’Gorr Talisman.\r\n".encode("cp1252")
            p.write_bytes(raw)
            lines = read_dump_lines(p)
            self.assertEqual(lines[0], "C\t\tObtain Ry’Gorr Talisman.")

    def test_utf8_bom_is_stripped(self):
        with TemporaryDirectory() as tmp:
            p = Path(tmp) / "dump.txt"
            p.write_bytes(b"\xef\xbb\xbfC\tPrimary Class Unlock - Bard\r\n")
            self.assertEqual(read_dump_lines(p)[0], "C\tPrimary Class Unlock - Bard")


if __name__ == "__main__":
    unittest.main()
