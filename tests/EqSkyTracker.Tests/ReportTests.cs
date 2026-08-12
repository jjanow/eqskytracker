using EqSkyTracker.Core;

namespace EqSkyTracker.Tests;

public class ReportTests
{
    private static readonly string Fixtures = Path.Combine(AppContext.BaseDirectory, "fixtures");
    private static readonly string Achievements = Path.Combine(Fixtures, "sample-Achievements.txt");
    private static readonly string Inventory = Path.Combine(Fixtures, "sample-Inventory.txt");
    private static readonly string HintsPath = Path.Combine(Fixtures, "sample-hints.json");

    [Fact]
    public void CharacterNameDerivedFromFilename()
    {
        CharacterReport report = Report.BuildReport(Achievements);
        Assert.Equal("sample", report.CharacterName);
    }

    [Fact]
    public void UnlockedCount()
    {
        CharacterReport report = Report.BuildReport(Achievements);
        Assert.Equal(1, report.UnlockedCount);
        Assert.Equal(2, report.TotalClasses);
    }

    [Fact]
    public void WorksWithoutInventory()
    {
        CharacterReport report = Report.BuildReport(Achievements);
        ClassReport warrior = report.Classes.First(c => c.ClassName == "TestWarrior");
        Assert.All(warrior.Items, i => Assert.False(i.InInventory));
    }

    [Fact]
    public void InInventoryCrossReference()
    {
        CharacterReport report = Report.BuildReport(Achievements, Inventory);
        ClassReport warrior = report.Classes.First(c => c.ClassName == "TestWarrior");
        ItemStatus dagas = warrior.Items.First(i => i.Name == "Dagas");
        Assert.False(dagas.InInventory); // not present anywhere in the fixture inventory
        ItemStatus belt = warrior.Items.First(i => i.Name == "Belt of the Four Winds");
        Assert.True(belt.InInventory);
    }

    [Fact]
    public void CompoundItemNameMatchesEitherHalf()
    {
        CharacterReport report = Report.BuildReport(Achievements, Inventory);
        ClassReport warrior = report.Classes.First(c => c.ClassName == "TestWarrior");
        ItemStatus compound = warrior.Items.First(i => i.Name == "Fangol and Spirit Blade");
        Assert.True(compound.InInventory); // "Fangol" half is present in bags
    }

    [Fact]
    public void HintsAreAttachedWhenSupplied()
    {
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        ClassReport warrior = report.Classes.First(c => c.ClassName == "TestWarrior");
        ItemStatus compound = warrior.Items.First(i => i.Name == "Fangol and Spirit Blade");
        Assert.NotNull(compound.Hint);
        Assert.Equal("Test NPC", compound.Hint.Npc);
    }

    [Fact]
    public void NoHintForUnlistedItem()
    {
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        ClassReport warrior = report.Classes.First(c => c.ClassName == "TestWarrior");
        ItemStatus dagas = warrior.Items.First(i => i.Name == "Dagas");
        Assert.Null(dagas.Hint);
    }

    [Fact]
    public void FarmedComponentFlaggedNeededWhenRewardIncomplete()
    {
        // "Fangol and Spirit Blade" is still incomplete in the fixture
        // achievements, and "Djinni War Blade" (its turn-in component) is
        // sitting in bags.
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        FarmedItemStatus blade = report.FarmedItems.First(f => f.Name == "Djinni War Blade");
        Assert.False(blade.SafeToSell);
        Assert.Equal(["Fangol and Spirit Blade"], blade.NeededFor);
    }

    [Fact]
    public void FarmedComponentFlaggedSafeToSellWhenRewardComplete()
    {
        // Belt of the Four Winds is already complete, so its leftover
        // turn-in component ("Fine Belt Buckle") is just clutter.
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        FarmedItemStatus buckle = report.FarmedItems.First(f => f.Name == "Fine Belt Buckle");
        Assert.True(buckle.SafeToSell);
        Assert.Empty(buckle.NeededFor);
    }

    [Fact]
    public void NoFarmedItemsWithoutInventory()
    {
        CharacterReport report = Report.BuildReport(Achievements, hintsPath: HintsPath);
        Assert.Empty(report.FarmedItems);
    }
}
