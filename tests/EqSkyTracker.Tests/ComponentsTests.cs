using EqSkyTracker.Core;

namespace EqSkyTracker.Tests;

public class ParseComponentsTests
{
    [Fact]
    public void SingleComponentPlusWindRune()
    {
        const string text = "Turn in Efreeti War Shield plus Wind Rune Heda to Sarkis Ebonblade to complete 'Shadow Knight Test of Envenoming' (reward: Obtenebrate Mithril Guard).";
        Assert.Equal(["Efreeti War Shield", "Wind Rune Heda"], Components.ParseComponents(text));
    }

    [Fact]
    public void MultipleComponentsStripIslandTags()
    {
        const string text = "Turn in Sphinx Claw (7-SotS), Mithril Bands (8-EoV), Brass Knuckles plus Wind Rune Izah to Animist Kratho to complete 'Beastlord Test of Claw' (reward: Windhowl, Spirit Render).";
        Assert.Equal(["Sphinx Claw", "Mithril Bands", "Brass Knuckles", "Wind Rune Izah"], Components.ParseComponents(text));
    }

    [Fact]
    public void UnrecognizedShapeReturnsEmpty()
    {
        Assert.Empty(Components.ParseComponents("Turn in a Test Component to Test NPC."));
    }
}

public class ParseComponentsWithTagsTests
{
    [Fact]
    public void PairsEachComponentWithItsOwnTag()
    {
        const string text = "Turn in Sphinx Claw (7-SotS), Mithril Bands (8-EoV), Brass Knuckles plus Wind Rune Izah to Animist Kratho to complete 'Beastlord Test of Claw' (reward: Windhowl, Spirit Render).";
        Assert.Equal(
            [("Sphinx Claw", "7-SotS"), ("Mithril Bands", "8-EoV"), ("Brass Knuckles", null), ("Wind Rune Izah", null)],
            Components.ParseComponentsWithTags(text));
    }

    [Fact]
    public void WindRuneNeverHasATag()
    {
        const string text = "Turn in Efreeti War Shield (2-PoS) plus Wind Rune Heda to Sarkis Ebonblade to complete 'Shadow Knight Test of Envenoming' (reward: Obtenebrate Mithril Guard).";
        List<(string Name, string? Tag)> result = Components.ParseComponentsWithTags(text);
        Assert.Equal(("Efreeti War Shield", "2-PoS"), result[0]);
        Assert.Equal(("Wind Rune Heda", null), result[1]);
    }

    [Fact]
    public void UnrecognizedShapeReturnsEmpty()
    {
        Assert.Empty(Components.ParseComponentsWithTags("Turn in a Test Component to Test NPC."));
    }
}
