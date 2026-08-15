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

    [Fact]
    public void MissingComponentCarriesItsIslandTagAndInventoryStatus()
    {
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        MissingComponentStatus blade = report.MissingComponents.First(c => c.Name == "Djinni War Blade");
        Assert.Equal("7-SotS", blade.Source);
        Assert.True(blade.InInventory);
        Assert.Equal(["Fangol and Spirit Blade"], blade.NeededFor);
    }

    [Fact]
    public void ComponentWithNoTagComesBackBlank()
    {
        // "Shiny Trinket" is a plus-clause component -- it never carries an
        // island-tag parenthetical in the source text.
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        MissingComponentStatus trinket = report.MissingComponents.First(c => c.Name == "Shiny Trinket");
        Assert.Equal("", trinket.Source);
    }

    [Fact]
    public void WindRunesExcludedFromMissingComponents()
    {
        // "Wind Rune Jaka" is named by the still-incomplete "Fangol and Spirit
        // Blade" reward, but Wind Runes live in an alternate-currency window
        // and never show up in an inventory dump, so they're excluded from the
        // trackable missing-items list entirely.
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        Assert.DoesNotContain(report.MissingComponents, c => c.Name.StartsWith("Wind Rune", StringComparison.Ordinal));
    }

    [Fact]
    public void WindRunesExcludedFromFarmedItems()
    {
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        Assert.DoesNotContain(report.FarmedItems, f => f.Name.StartsWith("Wind Rune", StringComparison.Ordinal));
    }

    [Fact]
    public void ComponentSharedByTwoIncompleteRewardsCollapsesToOneRowWithBothConsumers()
    {
        // "Gem of Invigoration" is named by both "Fangol and Spirit Blade"
        // and "Test Cross Item", both still incomplete in the fixture.
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        List<MissingComponentStatus> matches = [.. report.MissingComponents.Where(c => c.Name == "Gem of Invigoration")];
        MissingComponentStatus gem = Assert.Single(matches);
        Assert.Equal(["Fangol and Spirit Blade", "Test Cross Item"], gem.NeededFor);
        Assert.False(gem.InInventory); // not present anywhere in the fixture inventory
    }

    [Fact]
    public void ComponentDropsOffOnceEveryConsumingRewardIsComplete()
    {
        // "Fine Belt Buckle" is only named by "Belt of the Four Winds",
        // which is already complete in the fixture, so it shouldn't surface
        // as something still needed.
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        Assert.DoesNotContain(report.MissingComponents, c => c.Name == "Fine Belt Buckle");
    }

    [Fact]
    public void AutoCompletedClassIsVerifiedAgainstInventoryInsteadOfAchievementFlags()
    {
        // TestBard was unlocked by confirming Primary Class (not by doing the
        // quests), so the game's "C" on both "Obtain X." lines is unreliable.
        // "Mask of Song" sits in the fixture inventory's Equipment/keyring
        // list, but "Amulet of the Fae" does not -- despite both showing "C"
        // in the achievements dump.
        CharacterReport report = Report.BuildReport(Achievements, Inventory);
        ClassReport bard = report.Classes.First(c => c.ClassName == "TestBard");
        Assert.True(bard.AutoCompleted);
        Assert.True(bard.VerifiedFromInventory);

        ItemStatus mask = bard.Items.First(i => i.Name == "Mask of Song");
        Assert.True(mask.Complete);

        ItemStatus amulet = bard.Items.First(i => i.Name == "Amulet of the Fae");
        Assert.False(amulet.Complete);
        Assert.False(bard.RewardComplete);
    }

    [Fact]
    public void AutoCompletedClassFallsBackToAchievementFlagsWithoutInventory()
    {
        // Without an inventory dump there's nothing to verify against, so
        // the (unreliable) achievement flags are the best information
        // available.
        CharacterReport report = Report.BuildReport(Achievements);
        ClassReport bard = report.Classes.First(c => c.ClassName == "TestBard");
        Assert.True(bard.AutoCompleted);
        Assert.False(bard.VerifiedFromInventory);
        Assert.True(bard.Items.First(i => i.Name == "Amulet of the Fae").Complete);

        // Regression: every item reading "complete" (from untrustworthy achievement
        // flags) must NOT be reported as a genuinely complete reward set -- that's
        // the original false-"done" bug, just reached via the no-inventory path.
        Assert.False(bard.RewardComplete);
    }

    [Fact]
    public void RewardCompleteCountExcludesAutoCompletedClassesWithUnverifiedItems()
    {
        CharacterReport report = Report.BuildReport(Achievements, Inventory);
        Assert.Equal(0, report.RewardCompleteCount);
    }

    [Fact]
    public void NoHintMeansItemContributesNoMissingComponents()
    {
        // "Dagas" has no hint in the fixture, so it can't contribute
        // components -- it should simply be omitted, not throw or guess.
        CharacterReport report = Report.BuildReport(Achievements, Inventory, HintsPath);
        Assert.DoesNotContain(report.MissingComponents, c => c.NeededFor.Contains("Dagas"));
    }
}
