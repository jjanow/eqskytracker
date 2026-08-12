import unittest
from pathlib import Path
from tempfile import TemporaryDirectory
from unittest.mock import patch

from eqskytracker import discovery
from eqskytracker.discovery import find_characters


class TestConfigPersistence(unittest.TestCase):
    def test_saving_one_setting_does_not_clobber_another(self):
        # Regression test: save_last_dir() used to overwrite the whole
        # config.json, so saving window geometry after picking a folder (or
        # vice versa) would silently erase the other setting.
        with TemporaryDirectory() as tmp:
            cfg_dir = Path(tmp)
            with patch.object(discovery, "config_dir", return_value=cfg_dir):
                discovery.save_last_dir("/some/dir")
                discovery.save_window_geometry("900x600+10+10")

                self.assertEqual(discovery.load_last_dir(), Path("/some/dir"))
                self.assertEqual(discovery.load_window_geometry(), "900x600+10+10")

    def test_load_window_geometry_missing_returns_none(self):
        with TemporaryDirectory() as tmp:
            with patch.object(discovery, "config_dir", return_value=Path(tmp)):
                self.assertIsNone(discovery.load_window_geometry())

    def test_unwritable_config_dir_does_not_raise(self):
        # Regression test: if the app is installed somewhere the current
        # user can't write to (e.g. a system-wide install on Windows/macOS/
        # Linux), saving a setting used to raise OSError and crash whatever
        # triggered it (picking a folder, closing the window). Persisting
        # the setting is a convenience, not something the app should depend
        # on to keep running.
        with TemporaryDirectory() as tmp:
            unwritable = Path(tmp) / "readonly"
            unwritable.mkdir(mode=0o500)
            try:
                with patch.object(discovery, "config_dir", return_value=unwritable / "nested"):
                    discovery.save_last_dir("/some/dir")  # must not raise
            finally:
                unwritable.chmod(0o700)


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


class TestFindCharactersPermissionErrors(unittest.TestCase):
    def test_unreadable_directory_returns_empty_instead_of_raising(self):
        # Regression test: a candidate directory can exist but not be
        # listable (e.g. macOS TCC denying access to ~/Documents until the
        # user grants it). That must degrade to "no characters here", not
        # crash the app during startup's directory scan.
        with TemporaryDirectory() as tmp:
            directory = Path(tmp)
            with patch.object(Path, "iterdir", side_effect=PermissionError("denied")):
                self.assertEqual(find_characters(directory), [])

    def test_unreadable_entry_is_skipped_not_raised(self):
        with TemporaryDirectory() as tmp:
            directory = Path(tmp)
            (directory / "Someone_server-Achievements.txt").write_text("")
            with patch.object(Path, "is_file", side_effect=PermissionError("denied")):
                self.assertEqual(find_characters(directory), [])


if __name__ == "__main__":
    unittest.main()
