import unittest

from eqskytracker.components import parse_components


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


if __name__ == "__main__":
    unittest.main()
