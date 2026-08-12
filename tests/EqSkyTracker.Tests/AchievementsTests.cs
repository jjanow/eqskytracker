using EqSkyTracker.Core;

namespace EqSkyTracker.Tests;

public class AchievementsTests
{
    private static readonly string Fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-Achievements.txt");

    private readonly List<Achievement> _achievements;
    private readonly List<ClassUnlock> _unlocks;

    public AchievementsTests()
    {
        _achievements = Achievements.ParseAchievements(Fixture);
        _unlocks = Achievements.ClassUnlocks(_achievements);
    }

    [Fact]
    public void CategoriesAreNotAchievements()
    {
        List<string> names = [.. _achievements.Select(a => a.Name)];
        Assert.DoesNotContain("Untapped Potential: Classes", names);
        Assert.DoesNotContain("General: Keys", names);
    }

    [Fact]
    public void FindsBothClassUnlocks()
    {
        Assert.Equal(new HashSet<string> { "TestBard", "TestWarrior" }, _unlocks.Select(u => u.ClassName).ToHashSet());
    }

    [Fact]
    public void FullyCompleteClassIsUnlocked()
    {
        ClassUnlock bard = _unlocks.First(u => u.ClassName == "TestBard");
        Assert.True(bard.Unlocked);
        Assert.Equal(2, bard.ObtainedCount);
        Assert.Equal(2, bard.TotalCount);
    }

    [Fact]
    public void PartialClassIsNotUnlocked()
    {
        ClassUnlock warrior = _unlocks.First(u => u.ClassName == "TestWarrior");
        Assert.False(warrior.Unlocked);
        Assert.Equal(1, warrior.ObtainedCount);
        Assert.Equal(3, warrior.TotalCount);
    }

    [Fact]
    public void ItemNamesStripObtainPrefixAndPeriod()
    {
        ClassUnlock warrior = _unlocks.First(u => u.ClassName == "TestWarrior");
        HashSet<string?> names = [.. warrior.Items.Select(i => i.ItemName)];
        Assert.Equal(new HashSet<string?> { "Belt of the Four Winds", "Dagas", "Fangol and Spirit Blade" }, names);
    }

    [Fact]
    public void MetaLinesAreNotItemRequirements()
    {
        ClassUnlock warrior = _unlocks.First(u => u.ClassName == "TestWarrior");
        HashSet<string> texts = [.. warrior.Items.Select(r => r.Text)];
        Assert.DoesNotContain("This achievement can be bypassed using a Primary Class Unlock Token.", texts);
    }

    [Fact]
    public void NonClassAchievementsAreIgnoredByClassUnlocks()
    {
        Assert.DoesNotContain(_unlocks, u => u.ClassName == "Islands of Sky Keys");
    }
}
