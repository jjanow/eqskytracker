using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia.Media;

namespace EqSkyTracker.Gui;

/// <summary>A single row in the "By Class" tree -- either a class/farmed-items
/// group heading or a leaf item underneath it.</summary>
public class TreeNode(string name, string status, string detail, IBrush foreground) : INotifyPropertyChanged
{
    private bool _isExpanded;

    public string Name { get; } = name;
    public string Status { get; } = status;
    public string Detail { get; } = detail;
    public IBrush Foreground { get; } = foreground;
    public List<TreeNode> Children { get; } = [];

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (_isExpanded != value)
            {
                _isExpanded = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
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
