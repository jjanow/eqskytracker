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

public class ExtractIslandTagsTests
{
    [Fact]
    public void MultipleTagsInOrder()
    {
        const string text = "Turn in Sphinx Claw (7-SotS), Mithril Bands (8-EoV), Brass Knuckles plus Wind Rune Izah to Animist Kratho to complete 'Beastlord Test of Claw' (reward: Windhowl, Spirit Render).";
        Assert.Equal(["7-SotS", "8-EoV"], Components.ExtractIslandTags(text));
    }

    [Fact]
    public void NoTagsReturnsEmpty()
    {
        const string text = "Turn in Efreeti War Shield plus Wind Rune Heda to Sarkis Ebonblade to complete 'Shadow Knight Test of Envenoming' (reward: Obtenebrate Mithril Guard).";
        Assert.Empty(Components.ExtractIslandTags(text));
    }
}
