import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

from eqskytracker import discovery


class TestCandidateDirs(unittest.TestCase):
    def test_finds_per_game_wine_prefix_layout_without_remembered_dir(self):
        # Regression test: on a fresh install (no ~/.config/eqskytracker
        # config.json yet), candidate_dirs() must still find dump files
        # under a per-game Wine/Proton prefix layout like
        # ~/Games/EQLegends/drive_c/users/Public/Daybreak Game Company/
        # Installed Games/EverQuestLegends/ -- not just a single shared
        # prefix directly under ~/Games.
        with TemporaryDirectory() as tmp:
            fake_home = Path(tmp)
            game_dir = (
                fake_home / "Games" / "EQLegends" / "drive_c" / "users" / "Public"
                / "Daybreak Game Company" / "Installed Games" / "EverQuestLegends"
            )
            game_dir.mkdir(parents=True)
            (game_dir / "Someone_server-Achievements.txt").write_text("")

            with patch.object(discovery, "load_last_dir", return_value=None), \
                 patch.object(Path, "home", return_value=fake_home), \
                 patch.object(discovery.platform, "system", return_value="Linux"):
                discovery.os.environ.pop("EQSKYTRACKER_DIR", None)
                dirs = discovery.candidate_dirs()

            self.assertIn(game_dir.resolve(), dirs)
            characters = discovery.find_characters(game_dir)
            self.assertEqual(characters[0].name, "Someone_server")


if __name__ == "__main__":
    unittest.main()
