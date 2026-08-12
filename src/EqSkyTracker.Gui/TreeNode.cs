using Avalonia.Media;

namespace EqSkyTracker.Gui;

/// <summary>A single row in the "By Class" tree -- either a class/farmed-items
/// group heading or a leaf item underneath it.</summary>
public class TreeNode(string name, string status, string detail, IBrush foreground)
{
    public string Name { get; } = name;
    public string Status { get; } = status;
    public string Detail { get; } = detail;
    public IBrush Foreground { get; } = foreground;
    public List<TreeNode> Children { get; } = [];
}

/// <summary>A single row in the "All Missing Items" grid.</summary>
public class MissingItemRow(string item, string className, string status, string source, string detail)
{
    public string Item { get; } = item;
    public string ClassName { get; } = className;
    public string Status { get; } = status;
    public string Source { get; } = source;
    public string Detail { get; } = detail;
}
