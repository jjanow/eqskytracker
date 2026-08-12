// Parser for EverQuest-style "/outputfile inventory" dumps.
//
// The file has two tab-delimited sections separated by a blank line:
//
//     Location	Name	ID	Count	Slots
//     Any Slot	Eye of Innoruuk +5	20656	1	10
//     ...
//     <blank line>
//     KeyRing	Name	ID
//     Augmentation	Nightshade Wreath (Exaltation)	1408
//     ...
//
// Item names carry cosmetic suffixes (" +N" power-tiers, " (Exaltation)"
// augment-slot copies) that must be stripped before matching against quest/
// achievement item names, since "Spiroc Wingblade +2" and "Spiroc Wingblade
// (Exaltation)" both refer to item ID 20679.
using System.Text.RegularExpressions;

namespace EqSkyTracker.Core;

public static partial class ItemNaming
{
    [GeneratedRegex(@"\s*(\+\d+|\(Exaltation\))\s*$")]
    private static partial Regex SuffixRegex();

    /// <summary>Strip trailing ' +N' / ' (Exaltation)' decorations for name matching.</summary>
    public static string NormalizeItemName(string name)
    {
        string? prev = null;
        while (prev != name)
        {
            prev = name;
            name = SuffixRegex().Replace(name, "").Trim();
        }
        return name;
    }
}

public class InventoryItem
{
    public required string Location { get; init; }
    public required string Name { get; init; }
    public required int ItemId { get; init; }
    public required int Count { get; init; }
    public required int Slots { get; init; }

    public string NormalizedName => ItemNaming.NormalizeItemName(Name);
    public bool IsExaltationCopy => Name.Contains("(Exaltation)");
}

public class KeyringItem
{
    public required string Category { get; init; }
    public required string Name { get; init; }
    public required int ItemId { get; init; }

    public string NormalizedName => ItemNaming.NormalizeItemName(Name);
}

public class Inventory
{
    public required List<InventoryItem> Items { get; init; }
    public required List<KeyringItem> Keyring { get; init; }

    public List<InventoryItem> FindByName(string name)
    {
        string target = ItemNaming.NormalizeItemName(name);
        return Items.Where(i => string.Equals(i.NormalizedName, target, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public List<KeyringItem> FindInKeyring(string name)
    {
        string target = ItemNaming.NormalizeItemName(name);
        return Keyring.Where(k => string.Equals(k.NormalizedName, target, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public bool HasItem(string name) => FindByName(name).Count > 0 || FindInKeyring(name).Count > 0;
}

public static class InventoryParser
{
    public static Inventory ParseInventory(string path)
    {
        var items = new List<InventoryItem>();
        var keyring = new List<KeyringItem>();

        List<string> lines = DumpFile.ReadDumpLines(path);

        // Section 1: item slots, up to the first blank line.
        int sectionBreak = lines.Count;
        for (int idx = 0; idx < lines.Count; idx++)
        {
            if (lines[idx].Length == 0)
            {
                sectionBreak = idx;
                break;
            }
        }

        for (int i = 0; i < sectionBreak; i++)
        {
            string[] parts = lines[i].Split('\t');
            if (parts[0] == "Location")
            {
                continue; // header row
            }
            if (parts.Length < 5)
            {
                continue;
            }
            string location = parts[0], name = parts[1], itemIdRaw = parts[2], countRaw = parts[3], slotsRaw = parts[4];
            if (name == "Empty")
            {
                continue;
            }
            if (int.TryParse(itemIdRaw, out int itemId) &&
                int.TryParse(countRaw, out int count) &&
                int.TryParse(slotsRaw, out int slots))
            {
                items.Add(new InventoryItem { Location = location, Name = name, ItemId = itemId, Count = count, Slots = slots });
            }
        }

        // Section 2: keyring, after the blank line.
        for (int i = sectionBreak + 1; i < lines.Count; i++)
        {
            string line = lines[i];
            if (line.Length == 0)
            {
                continue;
            }
            string[] parts = line.Split('\t');
            if (parts[0] == "KeyRing")
            {
                continue; // header row
            }
            if (parts.Length < 3)
            {
                continue;
            }
            string category = parts[0], name = parts[1], itemIdRaw = parts[2];
            if (int.TryParse(itemIdRaw, out int itemId))
            {
                keyring.Add(new KeyringItem { Category = category, Name = name, ItemId = itemId });
            }
        }

        return new Inventory { Items = items, Keyring = keyring };
    }
}
