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
    private static readonly IBrush ReadyRowBrush = Brush.Parse("#4d7ec699");
    private static readonly IBrush GroupRowBrush = Brush.Parse("#26282b");
    private static readonly IBrush TransparentBrush = Brushes.Transparent;

    private const string FarmedGroupKey = "__farmed__";

    private enum ClassSortColumn { None, Name, Status }

    private readonly Button _chooseFolderButton;
    private readonly TextBlock _dirLabel;
    private readonly ComboBox _charCombo;
    private readonly Button _refreshButton;
    private readonly TextBlock _summaryLabel;
    private readonly DataGrid _classGrid;
    private readonly Button _classNameHeader;
    private readonly Button _classStatusHeader;
    private readonly DataGrid _missingGrid;
    private readonly DataGrid _readyGrid;
    private readonly TextBox _detailText;
    private readonly CheckBox _expandClassesCheckBox;

    private string? _currentDir;
    private List<Character> _characters = [];
    private CharacterReport? _report;
    private bool _suppressComboSelection;
    private ClassSortColumn _classSortColumn = ClassSortColumn.None;
    private bool _classSortAscending = true;
    private List<(ClassGridRow Group, List<ClassGridRow> Children)> _classGroups = [];
    private readonly HashSet<string> _collapsedGroups = new(StringComparer.Ordinal);
    private bool _resetGroupExpansion = true;

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
        _classGrid = this.FindControl<DataGrid>("ClassGrid")!;
        _classNameHeader = this.FindControl<Button>("ClassNameHeader")!;
        _classStatusHeader = this.FindControl<Button>("ClassStatusHeader")!;
        _missingGrid = this.FindControl<DataGrid>("MissingGrid")!;
        _readyGrid = this.FindControl<DataGrid>("ReadyGrid")!;
        _detailText = this.FindControl<TextBox>("DetailText")!;
        _expandClassesCheckBox = this.FindControl<CheckBox>("ExpandClassesCheckBox")!;

        _chooseFolderButton.Click += OnChooseFolderClick;
        _refreshButton.Click += (_, _) => ReloadCharacters();
        _charCombo.SelectionChanged += OnCharComboSelectionChanged;
        _classGrid.SelectionChanged += OnClassGridSelectionChanged;
        _classGrid.CellPointerPressed += OnClassGridCellPointerPressed;
        _classNameHeader.Click += (_, _) => OnClassGridHeaderClick(ClassSortColumn.Name);
        _classStatusHeader.Click += (_, _) => OnClassGridHeaderClick(ClassSortColumn.Status);
        _missingGrid.SelectionChanged += OnMissingGridSelectionChanged;
        _readyGrid.SelectionChanged += OnReadyGridSelectionChanged;
        _expandClassesCheckBox.IsCheckedChanged += OnExpandClassesCheckedChanged;
        Closing += OnClosing;

        ApplySavedGeometry();
        _expandClassesCheckBox.IsChecked = Discovery.LoadExpandClassesByDefault();
        UpdateClassGridHeaderLabels();

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
            _classGrid.ItemsSource = null;
            _missingGrid.ItemsSource = null;
            _readyGrid.ItemsSource = null;
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
        _resetGroupExpansion = true;
        RenderReport();
    }

    // -- expand-classes preference ---------------------------------------
    private void OnExpandClassesCheckedChanged(object? sender, RoutedEventArgs e)
    {
        bool expand = _expandClassesCheckBox.IsChecked ?? false;
        Discovery.SaveExpandClassesByDefault(expand);
        _resetGroupExpansion = true;
        if (_report is not null)
        {
            RenderReport();
        }
    }

    // -- rendering ------------------------------------------------------
    private void RenderReport()
    {
        CharacterReport report = _report!;
        _summaryLabel.Text = $"{report.CharacterName} — {report.RewardCompleteCount}/{report.TotalClasses} reward sets complete " +
                              $"({report.UnlockedCount} classes unlocked)";
        SetDetailText("Select an item below for its full pickup details.");

        var groups = new List<(ClassGridRow Group, List<ClassGridRow> Children)>();

        if (report.FarmedItems.Count > 0)
        {
            var farmedGroup = new ClassGridRow("Farmed items (Sky turn-ins)", "", "", DefaultBrush, GroupRowBrush, true, FarmedGroupKey) { IsPinned = true };
            var farmedChildren = new List<ClassGridRow>();
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
                farmedChildren.Add(new ClassGridRow($"   {f.Name} x{f.Count}", status, detail, color, TransparentBrush, false, FarmedGroupKey));
            }
            groups.Add((farmedGroup, farmedChildren));
        }

        foreach (ClassReport cls in report.Classes.OrderBy(c => c.RewardComplete).ThenBy(c => c.ClassName, StringComparer.Ordinal))
        {
            (string status, IBrush classColor) = DescribeClass(cls);
            var classGroup = new ClassGridRow(cls.ClassName, status, "", classColor, GroupRowBrush, true, cls.ClassName);
            var children = new List<ClassGridRow>();
            foreach (ItemStatus item in cls.Items)
            {
                (string itemStatus, string detail) = DescribeItem(item, cls.VerifiedFromInventory);
                IBrush color = item.Complete ? GreenBrush : AmberBrush;
                bool readyToTurnIn = !item.Complete && item.Readiness is { AllTrackableComponentsPresent: true };
                IBrush background = readyToTurnIn ? ReadyRowBrush : TransparentBrush;
                children.Add(new ClassGridRow("   " + item.Name, itemStatus, detail, color, background, false, cls.ClassName));
            }
            groups.Add((classGroup, children));
        }

        _classGroups = groups;
        SortClassGroups();

        if (_resetGroupExpansion)
        {
            bool expand = _expandClassesCheckBox.IsChecked ?? false;
            _collapsedGroups.Clear();
            if (!expand)
            {
                foreach ((ClassGridRow group, _) in _classGroups)
                {
                    _collapsedGroups.Add(group.GroupKey);
                }
            }
            _resetGroupExpansion = false;
        }

        RefreshClassGridRows();
        _missingGrid.ItemsSource = BuildMissingRows(report);
        _readyGrid.ItemsSource = BuildReadyRows(report);
    }

    /// <summary>
    /// Sorts the "By Class" grid's cached groups by the active header-clicked
    /// column, keeping the pinned "Farmed items" group first. No-op when no
    /// column is active, preserving the default reward-complete/class-name
    /// build order.
    /// </summary>
    private void SortClassGroups()
    {
        if (_classSortColumn == ClassSortColumn.None)
        {
            return;
        }

        int Compare(ClassGridRow a, ClassGridRow b)
        {
            string x = _classSortColumn == ClassSortColumn.Name ? a.Name.TrimStart() : a.Status;
            string y = _classSortColumn == ClassSortColumn.Name ? b.Name.TrimStart() : b.Status;
            int cmp = string.Compare(x, y, StringComparison.OrdinalIgnoreCase);
            return _classSortAscending ? cmp : -cmp;
        }

        List<(ClassGridRow Group, List<ClassGridRow> Children)> pinned = [.. _classGroups.Where(g => g.Group.IsPinned)];
        List<(ClassGridRow Group, List<ClassGridRow> Children)> rest = [.. _classGroups.Where(g => !g.Group.IsPinned)];
        rest.Sort((a, b) => Compare(a.Group, b.Group));
        _classGroups = [.. pinned, .. rest];

        foreach ((_, List<ClassGridRow> children) in _classGroups)
        {
            children.Sort(Compare);
        }
    }

    /// <summary>Flattens the cached groups into the grid's visible row list, skipping children of a collapsed group.</summary>
    private void RefreshClassGridRows()
    {
        var visible = new List<ClassGridRow>();
        foreach ((ClassGridRow group, List<ClassGridRow> children) in _classGroups)
        {
            bool collapsed = _collapsedGroups.Contains(group.GroupKey);
            group.Name = (collapsed ? "▸ " : "▾ ") + group.BaseName;
            visible.Add(group);
            if (!collapsed)
            {
                visible.AddRange(children);
            }
        }
        _classGrid.ItemsSource = visible;
    }

    /// <summary>
    /// Builds the "Ready to Turn In" grid rows -- still-incomplete class-unlock
    /// rewards whose trackable turn-in components are all in bags/bank/keyring.
    /// Wind Rune possession can't be confirmed from a dump, so a reward that
    /// still needs one is still listed, just distinguished by status/color.
    /// </summary>
    private static List<ReadyItemRow> BuildReadyRows(CharacterReport report) =>
    [
        .. report.Classes
            .SelectMany(cls => cls.Items.Select(item => (ClassName: cls.ClassName, Item: item)))
            .Where(x => !x.Item.Complete && x.Item.Readiness is { AllTrackableComponentsPresent: true })
            .OrderBy(x => x.ClassName, StringComparer.Ordinal)
            .ThenBy(x => x.Item.Name, StringComparer.Ordinal)
            .Select(x =>
            {
                TurnInReadiness readiness = x.Item.Readiness!;
                bool fullyReady = readiness.ReadyToTurnIn;
                string status = fullyReady ? "✓ Ready to turn in" : "Ready -- confirm Wind Rune in-game";
                string detail = x.Item.Hint?.HowToObtain ?? x.Item.Name;
                IBrush color = fullyReady ? GreenBrush : AmberBrush;
                return new ReadyItemRow(x.ClassName, x.Item.Name, x.Item.Hint?.Npc ?? "", status, detail, color);
            }),
    ];

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

    /// <summary>Returns (status text, tree color) for a class row, accounting for auto-completed (bypassed) unlocks.</summary>
    private static (string Status, IBrush Color) DescribeClass(ClassReport cls)
    {
        if (cls.RewardComplete)
        {
            return ("✓ Unlocked -- rewards complete", GreenBrush);
        }
        if (cls.AutoCompleted && !cls.VerifiedFromInventory)
        {
            return ($"⚠ Unlocked via shortcut -- add an inventory dump to verify ({cls.ObtainedCount}/{cls.TotalCount} per achievements, unreliable)", AmberBrush);
        }
        if (cls.AutoCompleted)
        {
            return ($"⚠ Unlocked via shortcut -- {cls.ObtainedCount}/{cls.TotalCount} rewards found in inventory", AmberBrush);
        }
        return ($"{cls.ObtainedCount}/{cls.TotalCount} items", DefaultBrush);
    }

    /// <summary>Returns (status text, full detail text) for a single item in the "By Class" tree.</summary>
    private static (string Status, string Detail) DescribeItem(ItemStatus item, bool verifiedFromInventory)
    {
        if (item.Complete)
        {
            string status = verifiedFromInventory ? "✓ confirmed in inventory" : "✓ obtained";
            return (status, item.Name);
        }
        var detailLines = new List<string> { item.Name };
        string needStatus = "needed";
        if (item.InInventory)
        {
            needStatus += "  (in bags/bank!)";
            detailLines.Add("Already sitting in your bags/bank/keyring.");
        }
        if (verifiedFromInventory)
        {
            detailLines.Add("Class was unlocked via shortcut -- not found in bags/bank/keyring/worn slots, so treated as not obtained.");
        }
        if (item.Hint is { Found: true, HowToObtain: { } howToObtain })
        {
            detailLines.Add(howToObtain);
        }
        else if (item.Hint is null)
        {
            detailLines.Add("No pickup hint available for this item yet.");
        }
        if (item.Readiness is { } readiness)
        {
            if (readiness.ReadyToTurnIn)
            {
                needStatus += "  -- ✓ ready to turn in";
                detailLines.Add("All turn-in components are in your bags/bank/keyring.");
            }
            else if (readiness.AllTrackableComponentsPresent)
            {
                needStatus += "  -- components ready, Wind Rune unverified";
                detailLines.Add("Every trackable component is in your bags/bank/keyring; still need to confirm the Wind Rune in your alternate-currency window.");
            }
            else
            {
                needStatus += $"  -- {readiness.Have}/{readiness.Total} components in bags";
                detailLines.Add($"{readiness.Have}/{readiness.Total} turn-in components are in your bags/bank/keyring so far.");
            }
        }
        return (needStatus, string.Join("\n", detailLines));
    }

    // -- "By Class" column sort -------------------------------------------
    private void OnClassGridHeaderClick(ClassSortColumn column)
    {
        if (_classSortColumn == column)
        {
            _classSortAscending = !_classSortAscending;
        }
        else
        {
            _classSortColumn = column;
            _classSortAscending = true;
        }
        UpdateClassGridHeaderLabels();
        if (_report is not null)
        {
            RenderReport();
        }
    }

    private void UpdateClassGridHeaderLabels()
    {
        string arrow = _classSortAscending ? " ▲" : " ▼";
        _classNameHeader.Content = "Name" + (_classSortColumn == ClassSortColumn.Name ? arrow : "");
        _classStatusHeader.Content = "Status" + (_classSortColumn == ClassSortColumn.Status ? arrow : "");
    }

    // -- "By Class" group expand/collapse ---------------------------------
    private void OnClassGridCellPointerPressed(object? sender, DataGridCellPointerPressedEventArgs e)
    {
        if (e.Row?.DataContext is ClassGridRow { IsGroup: true } row)
        {
            if (!_collapsedGroups.Add(row.GroupKey))
            {
                _collapsedGroups.Remove(row.GroupKey);
            }
            RefreshClassGridRows();
        }
    }

    private void OnClassGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_classGrid.SelectedItem is ClassGridRow row)
        {
            SetDetailText(row.Detail.Length > 0 ? row.Detail : "(class row -- select an item for details)");
        }
    }

    private void OnMissingGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_missingGrid.SelectedItem is MissingItemRow row)
        {
            SetDetailText(row.Detail);
        }
    }

    private void OnReadyGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_readyGrid.SelectedItem is ReadyItemRow row)
        {
            SetDetailText(row.Detail);
        }
    }

    private void SetDetailText(string text) => _detailText.Text = text;
}
