// Ties achievements + inventory + optional hints into a report the UIs render.
namespace EqSkyTracker.Core;

public class ItemStatus
{
    public required string Name { get; init; }
    public required bool Complete { get; init; }
    public required bool InInventory { get; init; }
    public required ItemHint? Hint { get; init; }
}

/// <summary>
/// A Plane of Sky turn-in component currently sitting in the player's
/// bags/bank/keyring, cross-referenced against every class-unlock reward
/// known to need it.
/// </summary>
public class FarmedItemStatus
{
    public required string Name { get; init; }
    public required int Count { get; init; }
    public required List<string> Locations { get; init; }

    /// <summary>Reward item names still incomplete that need this; [] means safe to sell/destroy.</summary>
    public required List<string> NeededFor { get; init; }

    public bool SafeToSell => NeededFor.Count == 0;
}

public class ClassReport
{
    public required string ClassName { get; init; }
    public required bool Unlocked { get; init; }
    public List<ItemStatus> Items { get; init; } = [];

    public int ObtainedCount => Items.Count(i => i.Complete);
    public int TotalCount => Items.Count;
}

public class CharacterReport
{
    public required string CharacterName { get; init; }
    public required List<ClassReport> Classes { get; init; }
    public List<FarmedItemStatus> FarmedItems { get; init; } = [];

    public int UnlockedCount => Classes.Count(c => c.Unlocked);
    public int TotalClasses => Classes.Count;
}

public static class Report
{
    private const string AchievementsSuffix = "-Achievements.txt";

    /// <summary>
    /// Some 'Obtain X' requirements name two items at once (e.g. 'Windhowl and
    /// Spirit Render'); treat those as satisfied if either half is present.
    /// </summary>
    private static bool HasAny(Inventory inventory, string name)
    {
        if (inventory.HasItem(name))
        {
            return true;
        }
        if (name.Contains(" and "))
        {
            return name.Split(" and ").Any(part => inventory.HasItem(part.Trim()));
        }
        return false;
    }

    private static string CharacterName(string achievementsPath)
    {
        string stem = Path.GetFileName(achievementsPath);
        return stem.EndsWith(AchievementsSuffix, StringComparison.Ordinal)
            ? stem[..^AchievementsSuffix.Length]
            : Path.GetFileNameWithoutExtension(stem);
    }

    public static CharacterReport BuildReport(string achievementsPath, string? inventoryPath = null, string? hintsPath = null)
    {
        List<Achievement> achievements = Achievements.ParseAchievements(achievementsPath);
        List<ClassUnlock> unlocks = Achievements.ClassUnlocks(achievements);

        Inventory? inventory = null;
        if (!string.IsNullOrEmpty(inventoryPath) && File.Exists(inventoryPath))
        {
            inventory = InventoryParser.ParseInventory(inventoryPath);
        }

        Dictionary<string, ItemHint> hints = Hints.LoadItemHints(hintsPath);

        var classes = new List<ClassReport>();
        foreach (ClassUnlock cu in unlocks)
        {
            var items = new List<ItemStatus>();
            foreach (Requirement req in cu.Items)
            {
                string name = req.ItemName ?? req.Text;
                items.Add(new ItemStatus
                {
                    Name = name,
                    Complete = req.Complete,
                    InInventory = inventory is not null && HasAny(inventory, name),
                    Hint = hints.GetValueOrDefault(name),
                });
            }
            classes.Add(new ClassReport { ClassName = cu.ClassName, Unlocked = cu.Unlocked, Items = items });
        }

        List<FarmedItemStatus> farmedItems = inventory is not null ? FarmedItemStatuses(inventory, classes) : [];

        return new CharacterReport
        {
            CharacterName = CharacterName(achievementsPath),
            Classes = classes,
            FarmedItems = farmedItems,
        };
    }

    /// <summary>
    /// Cross-reference bag/bank/keyring contents against the turn-in
    /// components (parsed from hint text) needed by every still-incomplete
    /// class-unlock item, so farmed loot can be flagged as still-needed or
    /// safe to sell/destroy.
    /// </summary>
    private static List<FarmedItemStatus> FarmedItemStatuses(Inventory inventory, List<ClassReport> classes)
    {
        var componentTargets = new Dictionary<string, List<(string Name, bool Complete)>>(StringComparer.OrdinalIgnoreCase);
        foreach (ClassReport cls in classes)
        {
            foreach (ItemStatus item in cls.Items)
            {
                if (item.Hint?.HowToObtain is not { } howToObtain)
                {
                    continue;
                }
                foreach (string component in Components.ParseComponents(howToObtain))
                {
                    if (!componentTargets.TryGetValue(component, out List<(string, bool)>? list))
                    {
                        list = [];
                        componentTargets[component] = list;
                    }
                    list.Add((item.Name, item.Complete));
                }
            }
        }

        var grouped = new Dictionary<string, (string Name, int Count, SortedSet<string> Locations)>(StringComparer.OrdinalIgnoreCase);
        var entries = new List<(string Name, int Count, string Location)>();
        entries.AddRange(inventory.Items.Select(i => (i.NormalizedName, i.Count, i.Location)));
        entries.AddRange(inventory.Keyring.Select(k => (k.NormalizedName, 1, k.Category)));

        foreach ((string name, int count, string location) in entries)
        {
            if (!componentTargets.ContainsKey(name))
            {
                continue;
            }
            if (!grouped.TryGetValue(name, out (string Name, int Count, SortedSet<string> Locations) g))
            {
                g = (name, 0, new SortedSet<string>(StringComparer.Ordinal));
                grouped[name] = g;
            }
            g.Locations.Add(location);
            grouped[name] = (g.Name, g.Count + count, g.Locations);
        }

        var statuses = new List<FarmedItemStatus>();
        foreach ((string key, (string name, int count, SortedSet<string> locations)) in grouped)
        {
            List<string> neededFor = [.. componentTargets[key]
                .Where(t => !t.Complete)
                .Select(t => t.Name)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(n => n, StringComparer.Ordinal)];
            statuses.Add(new FarmedItemStatus
            {
                Name = name,
                Count = count,
                Locations = [.. locations],
                NeededFor = neededFor,
            });
        }
        statuses.Sort((a, b) =>
        {
            int cmp = a.SafeToSell.CompareTo(b.SafeToSell);
            return cmp != 0 ? cmp : string.CompareOrdinal(a.Name, b.Name);
        });
        return statuses;
    }
}
