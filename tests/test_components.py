import unittest

from eqskytracker.components import extract_island_tags, parse_components


class TestParseComponents(unittest.TestCase):
    def test_single_component_plus_wind_rune(self):
        text = "Turn in Efreeti War Shield plus Wind Rune Heda to Sarkis Ebonblade to complete 'Shadow Knight Test of Envenoming' (reward: Obtenebrate Mithril Guard)."
        self.assertEqual(parse_components(text), ["Efreeti War Shield", "Wind Rune Heda"])

    def test_multiple_components_strip_island_tags(self):
        text = "Turn in Sphinx Claw (7-SotS), Mithril Bands (8-EoV), Brass Knuckles plus Wind Rune Izah to Animist Kratho to complete 'Beastlord Test of Claw' (reward: Windhowl, Spirit Render)."
        self.assertEqual(
            parse_components(text),
            ["Sphinx Claw", "Mithril Bands", "Brass Knuckles", "Wind Rune Izah"],
        )

    def test_unrecognized_shape_returns_empty(self):
        self.assertEqual(parse_components("Turn in a Test Component to Test NPC."), [])


class TestExtractIslandTags(unittest.TestCase):
    def test_multiple_tags_in_order(self):
        text = "Turn in Sphinx Claw (7-SotS), Mithril Bands (8-EoV), Brass Knuckles plus Wind Rune Izah to Animist Kratho to complete 'Beastlord Test of Claw' (reward: Windhowl, Spirit Render)."
        self.assertEqual(extract_island_tags(text), ["7-SotS", "8-EoV"])

    def test_no_tags_returns_empty(self):
        text = "Turn in Efreeti War Shield plus Wind Rune Heda to Sarkis Ebonblade to complete 'Shadow Knight Test of Envenoming' (reward: Obtenebrate Mithril Guard)."
        self.assertEqual(extract_island_tags(text), [])


if __name__ == "__main__":
    unittest.main()
