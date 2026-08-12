using EqSkyTracker.Core;

namespace EqSkyTracker.Tests;

public class InventoryTests
{
    private static readonly string Fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "sample-Inventory.txt");
    private readonly Inventory _inv = InventoryParser.ParseInventory(Fixture);

    [Fact]
    public void SkipsEmptySlots()
    {
        Assert.All(_inv.Items, i => Assert.NotEqual("Empty", i.Name));
    }

    [Fact]
    public void ParsesItemSlots()
    {
        Assert.Equal(6, _inv.Items.Count); // Empty slot excluded
    }

    [Fact]
    public void ParsesKeyringSeparately()
    {
        Assert.Single(_inv.Keyring);
        Assert.Equal("Mask of Song", _inv.Keyring[0].Name);
    }

    [Fact]
    public void HasItemMatchesBagItem()
    {
        Assert.True(_inv.HasItem("Belt of the Four Winds"));
    }

    [Fact]
    public void HasItemMatchesKeyringItem()
    {
        Assert.True(_inv.HasItem("Mask of Song"));
    }

    [Fact]
    public void HasItemFalseForMissing()
    {
        Assert.False(_inv.HasItem("Nonexistent Trinket"));
    }

    [Fact]
    public void NormalizeStripsPowerTierSuffix()
    {
        Assert.Equal("Spiroc Wingblade", ItemNaming.NormalizeItemName("Spiroc Wingblade +2"));
    }

    [Fact]
    public void NormalizeStripsExaltationSuffix()
    {
        Assert.Equal("Spiroc Wingblade", ItemNaming.NormalizeItemName("Spiroc Wingblade (Exaltation)"));
    }

    [Fact]
    public void PowerTierAndExaltationCopiesMatchSameName()
    {
        List<InventoryItem> matches = _inv.FindByName("Spiroc Wingblade");
        Assert.Equal(2, matches.Count);
        Assert.Equal(new HashSet<int> { 20679 }, matches.Select(m => m.ItemId).ToHashSet());
    }
}

public class SectionBreakEdgeCaseTests
{
    // Regression tests for reading raw bytes with an encoding-fallback path
    // (see DumpFileTests) -- that changes how a trailing newline turns into a
    // trailing "" entry in the line list, which is exactly what the
    // section-break scan keys off of.

    private static Inventory Parse(byte[] rawBytes)
    {
        string tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tmp, rawBytes);
            return InventoryParser.ParseInventory(tmp);
        }
        finally
        {
            File.Delete(tmp);
        }
    }

    [Fact]
    public void KeyringParsesWithTrailingNewline()
    {
        byte[] raw = "Location\tName\tID\tCount\tSlots\r\n"u8.ToArray()
            .Concat("Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"u8.ToArray())
            .Concat("\r\n"u8.ToArray())
            .Concat("KeyRing\tName\tID\r\n"u8.ToArray())
            .Concat("Augmentation\tMask of Song\t1408\r\n"u8.ToArray())
            .ToArray();
        Inventory inv = Parse(raw);
        Assert.Single(inv.Items);
        Assert.Single(inv.Keyring);
        Assert.Equal("Mask of Song", inv.Keyring[0].Name);
    }

    [Fact]
    public void KeyringParsesWithoutTrailingNewline()
    {
        byte[] raw = "Location\tName\tID\tCount\tSlots\r\n"u8.ToArray()
            .Concat("Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"u8.ToArray())
            .Concat("\r\n"u8.ToArray())
            .Concat("KeyRing\tName\tID\r\n"u8.ToArray())
            .Concat("Augmentation\tMask of Song\t1408"u8.ToArray()) // no trailing newline at EOF
            .ToArray();
        Inventory inv = Parse(raw);
        Assert.Single(inv.Keyring);
        Assert.Equal("Mask of Song", inv.Keyring[0].Name);
    }

    [Fact]
    public void KeyringParsesWithExtraBlankLineBeforeHeader()
    {
        byte[] raw = "Location\tName\tID\tCount\tSlots\r\n"u8.ToArray()
            .Concat("Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"u8.ToArray())
            .Concat("\r\n"u8.ToArray())
            .Concat("\r\n"u8.ToArray())
            .Concat("KeyRing\tName\tID\r\n"u8.ToArray())
            .Concat("Augmentation\tMask of Song\t1408\r\n"u8.ToArray())
            .ToArray();
        Inventory inv = Parse(raw);
        Assert.Single(inv.Keyring);
    }

    [Fact]
    public void NoKeyringSectionAtAll()
    {
        byte[] raw = "Location\tName\tID\tCount\tSlots\r\n"u8.ToArray()
            .Concat("Any Slot\tBelt of the Four Winds\t11673\t1\t10\r\n"u8.ToArray())
            .ToArray();
        Inventory inv = Parse(raw);
        Assert.Single(inv.Items);
        Assert.Empty(inv.Keyring);
    }
}
