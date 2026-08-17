using Avalonia.Media;

namespace EqSkyTracker.Gui;

/// <summary>A single row in the "By Class" grid -- either a class/farmed-items
/// group heading or a leaf item underneath it. Child rows are hidden from the
/// grid's ItemsSource while their group is collapsed.</summary>
public class ClassGridRow(string name, string status, string detail, IBrush foreground, IBrush background, bool isGroup, string groupKey)
{
    /// <summary>Plain label with no expand-arrow prefix; group rows keep this fixed while <see cref="Name"/> gets re-prefixed on every expand/collapse toggle.</summary>
    public string BaseName { get; } = name;

    /// <summary>Displayed label -- for group rows, refreshed with a ▸/▾ prefix each time the grid's visible rows are rebuilt.</summary>
    public string Name { get; set; } = name;

    public string Status { get; } = status;
    public string Detail { get; } = detail;
    public IBrush Foreground { get; } = foreground;

    /// <summary>Row-wide background -- used both for the group-heading stripe and for highlighting a reward that's ready to turn in.</summary>
    public IBrush Background { get; } = background;

    public bool IsGroup { get; } = isGroup;

    /// <summary>Identifies which group a child row belongs to, and which group a heading row toggles.</summary>
    public string GroupKey { get; } = groupKey;

    /// <summary>True for the "Farmed items" group heading, which always stays first regardless of column sort.</summary>
    public bool IsPinned { get; set; }
}

/// <summary>A single row in the "All Missing Items" grid -- a turn-in
/// component still needed by at least one incomplete class-unlock reward.</summary>
public class MissingItemRow(string item, string source, string neededFor, string inBags, string detail, IBrush foreground)
{
    public string Item { get; } = item;
    public string Source { get; } = source;
    public string NeededFor { get; } = neededFor;
    public string InBags { get; } = inBags;
    public string Detail { get; } = detail;
    public IBrush Foreground { get; } = foreground;
}

/// <summary>A single row in the "Ready to Turn In" grid -- a still-incomplete
/// class-unlock reward whose trackable turn-in components are all in
/// bags/bank/keyring (Wind Rune possession can't be confirmed from a dump).</summary>
public class ReadyItemRow(string className, string item, string npc, string components, string rune, string status, string detail, IBrush foreground)
{
    public string ClassName { get; } = className;
    public string Item { get; } = item;
    public string Npc { get; } = npc;

    /// <summary>The non-Wind-Rune components handed over for this turn-in (e.g. "Sphinx Claw, Efreeti War Axe").</summary>
    public string Components { get; } = components;

    /// <summary>The Wind Rune this turn-in needs (e.g. "Wind Rune Izah"), or "" if none.</summary>
    public string Rune { get; } = rune;

    public string Status { get; } = status;
    public string Detail { get; } = detail;
    public IBrush Foreground { get; } = foreground;
}
