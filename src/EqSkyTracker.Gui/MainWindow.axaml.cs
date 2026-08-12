using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using EqSkyTracker.Core;

namespace EqSkyTracker.Gui;

public partial class MainWindow : Window
{
    private static readonly IBrush GreenBrush = Brush.Parse("#7ec699");
    private static readonly IBrush AmberBrush = Brush.Parse("#e0b350");
    private static readonly IBrush RedBrush = Brush.Parse("#e08787");
    private static readonly IBrush DefaultBrush = Brush.Parse("#e6e6e6");

    private readonly Button _chooseFolderButton;
    private readonly TextBlock _dirLabel;
    private readonly ComboBox _charCombo;
    private readonly Button _refreshButton;
    private readonly TextBlock _summaryLabel;
    private readonly TreeView _classTree;
    private readonly DataGrid _missingGrid;
    private readonly TextBox _detailText;
    private readonly CheckBox _expandClassesCheckBox;

    private string? _currentDir;
    private List<Character> _characters = [];
    private CharacterReport? _report;
    private bool _suppressComboSelection;

    public MainWindow() : this(null)
    {
    }

    public MainWindow(string? initialDir)
    {
        InitializeComponent();

        _chooseFolderButton = this.FindControl<Button>("ChooseFolderButton")!;
        _dirLabel = this.FindControl<TextBlock>("DirLabel")!;
        _charCombo = this.FindControl<ComboBox>("CharCombo")!;
        _refreshButton = this.FindControl<Button>("RefreshButton")!;
        _summaryLabel = this.FindControl<TextBlock>("SummaryLabel")!;
        _classTree = this.FindControl<TreeView>("ClassTree")!;
        _missingGrid = this.FindControl<DataGrid>("MissingGrid")!;
        _detailText = this.FindControl<TextBox>("DetailText")!;
        _expandClassesCheckBox = this.FindControl<CheckBox>("ExpandClassesCheckBox")!;

        _chooseFolderButton.Click += OnChooseFolderClick;
        _refreshButton.Click += (_, _) => ReloadCharacters();
        _charCombo.SelectionChanged += OnCharComboSelectionChanged;
        _classTree.SelectionChanged += OnClassTreeSelectionChanged;
        _missingGrid.SelectionChanged += OnMissingGridSelectionChanged;
        _expandClassesCheckBox.IsCheckedChanged += OnExpandClassesCheckedChanged;
        Closing += OnClosing;

        ApplySavedGeometry();
        _expandClassesCheckBox.IsChecked = Discovery.LoadExpandClassesByDefault();

        _currentDir = initialDir;
        ReloadCharacters();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    // -- window geometry --------------------------------------------------
    private void ApplySavedGeometry()
    {
        string? saved = Discovery.LoadWindowGeometry();
        if (saved is not null)
        {
            string[] parts = saved.Split(',');
            if (parts.Length == 4 &&
                double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double w) &&
                double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double h) &&
                int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) &&
                int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y) &&
                w > 0 && h > 0)
            {
                Width = w;
                Height = h;
                WindowStartupLocation = WindowStartupLocation.Manual;
                Position = new PixelPoint(x, y);
                return;
            }
        }
        Width = 900;
        Height = 600;
    }

    private void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (WindowState == WindowState.Normal)
        {
            string geometry = string.Create(CultureInfo.InvariantCulture,
                $"{Width:F0},{Height:F0},{Position.X},{Position.Y}");
            Discovery.SaveWindowGeometry(geometry);
        }
    }

    // -- data loading -------------------------------------------------------
    private async void OnChooseFolderClick(object? sender, RoutedEventArgs e)
    {
        IStorageProvider? storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return;
        }
        IReadOnlyList<IStorageFolder> folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select folder containing your EQ dump files",
            AllowMultiple = false,
        });
        string? chosen = folders.Count > 0 ? folders[0].TryGetLocalPath() : null;
        if (chosen is not null)
        {
            _currentDir = chosen;
            Discovery.SaveLastDir(chosen);
            ReloadCharacters();
        }
    }

    private void ReloadCharacters()
    {
        List<string> dirs = _currentDir is not null ? [_currentDir] : Discovery.CandidateDirs();
        _characters = Discovery.FindAllCharacters(dirs);

        if (_currentDir is not null)
        {
            _dirLabel.Text = _currentDir;
        }
        else if (dirs.Count > 0)
        {
            _dirLabel.Text = $"(auto-detected: {dirs[0]})";
        }
        else
        {
            _dirLabel.Text = "(no folder selected)";
        }

        List<string> names = [.. _characters.Where(c => c.AchievementsPath is not null).Select(c => c.Name)];

        _suppressComboSelection = true;
        _charCombo.ItemsSource = names;
        if (names.Count > 0)
        {
            string current = _charCombo.SelectedItem as string ?? "";
            _charCombo.SelectedItem = names.Contains(current) ? current : names[0];
        }
        else
        {
            _charCombo.SelectedItem = null;
        }
        _suppressComboSelection = false;

        if (names.Count > 0)
        {
            LoadSelectedCharacter();
        }
        else
        {
            _summaryLabel.Text = "No character dumps found. Run '/outputfile achievements' " +
                                  "and '/outputfile inventory' in-game, then choose that folder.";
            _classTree.ItemsSource = null;
            _missingGrid.ItemsSource = null;
            SetDetailText("Select an item below for its full pickup details.");
        }
    }

    private void OnCharComboSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!_suppressComboSelection)
        {
            LoadSelectedCharacter();
        }
    }

    private void LoadSelectedCharacter()
    {
        string? name = _charCombo.SelectedItem as string;
        Character? match = _characters.FirstOrDefault(c => c.Name == name);
        if (match?.AchievementsPath is null)
        {
            return;
        }
        try
        {
            _report = Report.BuildReport(match.AchievementsPath, match.InventoryPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            new ErrorDialog("Failed to read dump", ex.Message).ShowDialog(this);
            return;
        }
        RenderReport();
    }

    // -- expand-classes preference ---------------------------------------
    private void OnExpandClassesCheckedChanged(object? sender, RoutedEventArgs e)
    {
        bool expand = _expandClassesCheckBox.IsChecked ?? false;
        Discovery.SaveExpandClassesByDefault(expand);
        if (_classTree.ItemsSource is IEnumerable<TreeNode> nodes)
        {
            foreach (TreeNode node in nodes)
            {
                node.IsExpanded = expand;
            }
        }
    }

    // -- rendering ------------------------------------------------------
    private void RenderReport()
    {
        CharacterReport report = _report!;
        _summaryLabel.Text = $"{report.CharacterName} — {report.UnlockedCount}/{report.TotalClasses} classes unlocked";
        SetDetailText("Select an item below for its full pickup details.");

        var rootNodes = new List<TreeNode>();

        if (report.FarmedItems.Count > 0)
        {
            var farmedNode = new TreeNode("Farmed items (Sky turn-ins)", "", "", DefaultBrush);
            foreach (FarmedItemStatus f in report.FarmedItems)
            {
                string where = string.Join(", ", f.Locations);
                string status;
                string detail;
                IBrush color;
                if (f.SafeToSell)
                {
                    status = "safe to sell/destroy";
                    detail = $"Not needed for anything still incomplete.\n{where}";
                    color = GreenBrush;
                }
                else
                {
                    status = "KEEP -- needed";
                    detail = $"Needed for: {string.Join(", ", f.NeededFor)}\n{where}";
                    color = RedBrush;
                }
                farmedNode.Children.Add(new TreeNode($"   {f.Name} x{f.Count}", status, detail, color));
            }
            rootNodes.Add(farmedNode);
        }

        foreach (ClassReport cls in report.Classes.OrderBy(c => c.Unlocked).ThenBy(c => c.ClassName, StringComparer.Ordinal))
        {
            string status = cls.Unlocked ? "✓ Unlocked" : $"{cls.ObtainedCount}/{cls.TotalCount} items";
            var classNode = new TreeNode(cls.ClassName, status, "", cls.Unlocked ? GreenBrush : DefaultBrush);
            foreach (ItemStatus item in cls.Items)
            {
                (string itemStatus, string detail) = DescribeItem(item);
                IBrush color = item.Complete ? GreenBrush : AmberBrush;
                classNode.Children.Add(new TreeNode("   " + item.Name, itemStatus, detail, color));
            }
            rootNodes.Add(classNode);
        }

        bool expand = _expandClassesCheckBox.IsChecked ?? false;
        foreach (TreeNode node in rootNodes)
        {
            node.IsExpanded = expand;
        }

        _classTree.ItemsSource = rootNodes;
        _missingGrid.ItemsSource = BuildMissingRows(report);
    }

    /// <summary>
    /// Builds the "All Missing Items" grid rows from the report's
    /// deduplicated turn-in components, using the same green/red
    /// have-it-vs-still-need coloring as the "Farmed items" tree node.
    /// </summary>
    private static List<MissingItemRow> BuildMissingRows(CharacterReport report) =>
    [
        .. report.MissingComponents.Select(c =>
        {
            string inBags = c.InInventory ? "yes" : "no";
            string detail = $"{c.Name}\nNeeded for: {string.Join(", ", c.NeededFor)}" +
                             (c.InInventory ? "\nAlready sitting in your bags/bank/keyring." : "");
            IBrush color = c.InInventory ? GreenBrush : RedBrush;
            return new MissingItemRow(c.Name, c.Source, string.Join(", ", c.NeededFor), inBags, detail, color);
        }),
    ];

    /// <summary>Returns (status text, full detail text) for a single item in the "By Class" tree.</summary>
    private static (string Status, string Detail) DescribeItem(ItemStatus item)
    {
        if (item.Complete)
        {
            return ("✓ obtained", item.Name);
        }
        var detailLines = new List<string> { item.Name };
        string status = "needed";
        if (item.InInventory)
        {
            status += "  (in bags/bank!)";
            detailLines.Add("Already sitting in your bags/bank/keyring.");
        }
        if (item.Hint is { Found: true, HowToObtain: { } howToObtain })
        {
            detailLines.Add(howToObtain);
        }
        else if (item.Hint is null)
        {
            detailLines.Add("No pickup hint available for this item yet.");
        }
        return (status, string.Join("\n", detailLines));
    }

    private void OnClassTreeSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classTree.SelectedItem is TreeNode node)
        {
            SetDetailText(node.Detail.Length > 0 ? node.Detail : "(class row -- select an item for details)");
        }
    }

    private void OnMissingGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_missingGrid.SelectedItem is MissingItemRow row)
        {
            SetDetailText(row.Detail);
        }
    }

    private void SetDetailText(string text) => _detailText.Text = text;
}
