using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Terminal.Gui.Drawing;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

public sealed class CoresTab : FrameView
{
    private static readonly string[] NewCoreLabels = { "Ask for each", "Install automatically", "Skip" };
    private const string DetailsGlyph = "[view]";
    private const string OnHeader = " ";

    private static string CheckedGlyph => Glyphs.CheckStateChecked.ToString();
    private static string UncheckedGlyph => Glyphs.CheckStateUnChecked.ToString();

    private readonly TuiContext context;
    private readonly OptionSelector newCoreSelector;
    private readonly Label hintLabel;
    private readonly TextField filterField;
    private readonly DropDownList categoryDropdown;
    private readonly DropDownList platformDropdown;
    private readonly TableView table;

    private IReadOnlyList<Core> cores = Array.Empty<Core>();
    private readonly List<Core> visible = new();
    private readonly HashSet<string> checkedIds = new();

    private List<(string Key, string Display)> categoryChoices = new();
    private List<string> categoryChoiceLabels = new();
    private List<(string Key, string Display)> platformChoices = new();
    private List<string> platformChoiceLabels = new();
    private string selectedCategory;
    private string selectedPlatform;
    private string query = string.Empty;
    private bool dirty;
    private bool loading;

    private int onColIndex;
    private int detailsColIndex;

    public CoresTab(TuiContext context)
    {
        this.context = context;
        Title = "Cores";

        var newCoreLabel = new Label { X = 1, Y = 0, Text = "New cores installed by default:" };

        newCoreSelector = new OptionSelector
        {
            X = Pos.Right(newCoreLabel) + 1,
            Y = 0,
            Width = Dim.Fill(),
            Orientation = Orientation.Horizontal,
            Labels = NewCoreLabels
        };

        newCoreSelector.ValueChanged += (_, _) => MarkDirty();

        var filterLabel = new Label { X = 1, Y = 2, Text = "Filter:" };

        // A real input box rather than type-ahead on the table: the shell's single-key tab/item
        // accelerators fire before the focused view, so typing a letter into the table jumped tabs.
        // TuiShell stands down for text entry, so every key belongs to this field while it has focus.
        filterField = new TextField { X = Pos.Right(filterLabel) + 1, Y = 2, Width = 30 };

        filterField.ValueChanged += (_, _) =>
        {
            if (loading)
            {
                return;
            }

            query = filterField.Text ?? string.Empty;
            RebuildTable();
        };

        filterField.KeyDown += (_, key) =>
        {
            if (key == Key.Esc)
            {
                if (filterField.Text?.Length > 0)
                {
                    filterField.Text = string.Empty;
                }

                table.SetFocus();
                key.Handled = true;
            }
            else if (key == Key.Enter || key == Key.CursorDown)
            {
                table.SetFocus();
                key.Handled = true;
            }
        };

        hintLabel = new Label { X = Pos.Right(filterField) + 2, Y = 2, Width = Dim.Fill(1) };

        var categoryLabel = new Label { X = 1, Y = 4, Text = "Category:" };
        categoryDropdown = new DropDownList { X = Pos.Right(categoryLabel) + 1, Y = 4, Width = 26 };
        categoryDropdown.KeyBindings.Remove(Key.CursorUp);
        categoryDropdown.KeyBindings.Remove(Key.CursorDown);
        categoryDropdown.ValueChanged += (_, _) =>
        {
            int index = categoryChoiceLabels.IndexOf(categoryDropdown.Text);
            selectedCategory = index <= 0 ? null : categoryChoices[index - 1].Key;
            RebuildTable();
            FocusTableSoon();
        };

        var platformLabel = new Label { X = Pos.Right(categoryDropdown) + 4, Y = 4, Text = "Platform:" };
        platformDropdown = new DropDownList { X = Pos.Right(platformLabel) + 1, Y = 4, Width = 44 };
        platformDropdown.KeyBindings.Remove(Key.CursorUp);
        platformDropdown.KeyBindings.Remove(Key.CursorDown);
        platformDropdown.ValueChanged += (_, _) =>
        {
            int index = platformChoiceLabels.IndexOf(platformDropdown.Text);
            selectedPlatform = index <= 0 ? null : platformChoices[index - 1].Key;
            RebuildTable();
            FocusTableSoon();
        };

        table = new TableView
        {
            X = 1,
            Y = 6,
            Width = Dim.Fill(1),
            Height = Dim.Fill(2),
            FullRowSelect = true,
            MultiSelect = false
        };
        table.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;
        table.Style.ShowHorizontalBottomLine = false;
        table.Style.ExpandLastColumn = true;
        table.Style.AlwaysShowHeaders = true;

        table.MouseEvent += OnTableMouse;
        table.KeyDown += OnTableKey;

        var selectAll = new Button { X = 0, Y = Pos.AnchorEnd(1), Text = "Select _all" };
        selectAll.Accepting += (_, e) => { e.Handled = true; SetAllVisible(true); };

        var clearAll = new Button { X = Pos.Right(selectAll) + 1, Y = Pos.AnchorEnd(1), Text = "C_lear all" };
        clearAll.Accepting += (_, e) => { e.Handled = true; SetAllVisible(false); };

        var save = new Button { X = Pos.Center(), Y = Pos.AnchorEnd(1), Text = "_Save" };
        save.Accepting += (_, e) => { e.Handled = true; Save(); };

        var revert = new Button { X = Pos.Right(save) + 2, Y = Pos.AnchorEnd(1), Text = "_Revert" };
        revert.Accepting += (_, e) =>
        {
            e.Handled = true;
            Refresh();
            TuiApp.PostStatus("Reverted to saved core selection.");
        };

        Add(newCoreLabel, newCoreSelector, filterLabel, filterField, hintLabel,
            categoryLabel, categoryDropdown, platformLabel, platformDropdown,
            table, selectAll, clearAll, save, revert);
    }

    /// <summary>
    /// Called when the tab is opened. Reloads from the saved state only when nothing is staged -
    /// arrowing off the tab and back must not discard toggles you haven't saved yet (issue #517).
    /// </summary>
    public void Activated()
    {
        if (dirty)
        {
            FocusTableSoon();
            return;
        }

        Refresh();
    }

    /// <summary>Load the core list and reset the controls to the saved state (also used by Revert).</summary>
    public void Refresh()
    {
        loading = true;

        try
        {
            cores = ServiceHelper.CoresService.Cores;
        }
        catch (Exception ex)
        {
            cores = Array.Empty<Core>();
            TuiApp.PostStatus($"Couldn't load the cores list: {ex.Message}");
        }

        checkedIds.Clear();

        foreach (var core in cores)
        {
            if (!ServiceHelper.SettingsService.GetCoreSettings(core.id).skip)
            {
                checkedIds.Add(core.id);
            }
        }

        query = string.Empty;
        filterField.Text = string.Empty;
        selectedCategory = null;
        selectedPlatform = null;

        categoryChoices = BuildChoices(CoreSelectorDialog.CategoryKeys(cores), CoreSelectorDialog.CategoryDisplay(cores));
        categoryChoiceLabels = ToChoiceLabels(categoryChoices);
        categoryDropdown.Source = new ListWrapper<string>(new ObservableCollection<string>(categoryChoiceLabels));
        categoryDropdown.Value = categoryChoiceLabels[0];

        platformChoices = BuildChoices(CoreSelectorDialog.PlatformKeys(cores), CoreSelectorDialog.PlatformDisplay(cores));
        platformChoiceLabels = ToChoiceLabels(platformChoices);
        platformDropdown.Source = new ListWrapper<string>(new ObservableCollection<string>(platformChoiceLabels));
        platformDropdown.Value = platformChoiceLabels[0];

        newCoreSelector.Value = ServiceHelper.SettingsService.Config.download_new_cores?.ToLowerInvariant() switch
        {
            "yes" => 1,
            "no" => 2,
            _ => 0
        };

        dirty = false;
        loading = false;

        RebuildTable();
        FocusTableSoon();
    }

    private void MarkDirty()
    {
        if (loading)
        {
            return;
        }

        dirty = true;
        UpdateHint();
    }

    private void RebuildTable()
    {
        visible.Clear();

        foreach (var core in cores)
        {
            bool matchText = query.Length == 0
                || CoreSelectorDialog.Label(core).Contains(query, StringComparison.OrdinalIgnoreCase);
            bool matchCategory = selectedCategory == null
                || string.Equals(CategoryKey(core), selectedCategory, StringComparison.Ordinal);
            bool matchPlatform = selectedPlatform == null
                || string.Equals(PlatformKey(core), selectedPlatform, StringComparison.Ordinal);

            if (matchText && matchCategory && matchPlatform)
            {
                visible.Add(core);
            }
        }

        var columns = new Dictionary<string, Func<Core, object>>
        {
            [OnHeader] = c => checkedIds.Contains(c.id) ? CheckedGlyph : UncheckedGlyph,
            ["Core"] = c => CoreSelectorDialog.Label(c),
            ["Platform"] = c => c.platform?.name ?? string.Empty,
            ["Category"] = c => c.platform?.category ?? string.Empty,
            ["Details"] = _ => DetailsGlyph
        };

        var source = new EnumerableTableSource<Core>(visible.ToList(), columns);
        onColIndex = Array.IndexOf(source.ColumnNames, OnHeader);
        detailsColIndex = Array.IndexOf(source.ColumnNames, "Details");

        table.Style.ColumnStyles[onColIndex] = FixedColumn(5, Alignment.Center);
        table.Style.ColumnStyles[Array.IndexOf(source.ColumnNames, "Core")] = FixedColumn(36);
        table.Style.ColumnStyles[Array.IndexOf(source.ColumnNames, "Platform")] = FixedColumn(28);
        table.Style.ColumnStyles[Array.IndexOf(source.ColumnNames, "Category")] = FixedColumn(13);
        table.Style.ColumnStyles[detailsColIndex] = FixedColumn(8);

        table.Table = source;

        if (visible.Count > 0)
        {
            table.SetSelection(Math.Max(onColIndex, 0), 0, false);
        }

        UpdateHint();
    }

    private void UpdateHint()
    {
        string note = query.Length == 0
            ? "Space toggles · Enter = info · / to filter"
            : $"{visible.Count}/{cores.Count} shown · Esc clears";

        hintLabel.Text = note + (dirty ? "   - unsaved changes" : string.Empty);
    }

    private void OnTableMouse(object sender, Mouse mouse)
    {
        // TableView ignores the wheel in Terminal.Gui 2.4.12 (ListView handles it, which is why the
        // other tabs scroll and this one didn't), so drive RowOffset ourselves. Handled first: the
        // hit-testing below would otherwise drag the selection to whatever row is under the pointer.
        bool wheelDown = mouse.Flags.HasFlag(MouseFlags.WheeledDown);

        if (wheelDown || mouse.Flags.HasFlag(MouseFlags.WheeledUp))
        {
            table.RowOffset += wheelDown ? 1 : -1;
            table.EnsureValidScrollOffsets();
            table.SetNeedsDraw();
            mouse.Handled = true;
            return;
        }

        if (mouse.Position is not { } pos)
        {
            return;
        }

        var hit = table.ScreenToCell(pos.X, pos.Y, out int? header, out int? offsetX);

        if (header != null || hit is not { } cell || cell.Y < 0 || cell.Y >= visible.Count)
        {
            return;
        }

        table.SetSelection(cell.X, cell.Y, false);

        if (!mouse.Flags.HasFlag(MouseFlags.LeftButtonClicked))
        {
            return;
        }

        bool clickedView = cell.X == detailsColIndex && offsetX is { } ox && ox < DetailsGlyph.Length;

        if (clickedView)
        {
            CoreDetailsModal.Show(visible[cell.Y]);
        }
        else
        {
            ToggleCore(visible[cell.Y]);
        }

        mouse.Handled = true;
    }

    private void OnTableKey(object sender, Key key)
    {
        if (key == Key.Enter)
        {
            if (CurrentRow is { } row)
            {
                CoreDetailsModal.Show(visible[row]);
            }

            key.Handled = true;
        }
        else if (key == Key.Space)
        {
            if (CurrentRow is { } row)
            {
                ToggleCore(visible[row]);
            }

            key.Handled = true;
        }
        else if (key == Key.Esc)
        {
            if (query.Length > 0)
            {
                filterField.Text = string.Empty;
                key.Handled = true;
            }
        }
        else if (key.AsRune.Value == '/')
        {
            filterField.SetFocus();
            key.Handled = true;
        }
    }

    private int? CurrentRow
    {
        get
        {
            int row = table.GetAllSelectedCells().Select(cell => cell.Y).DefaultIfEmpty(-1).First();
            return row >= 0 && row < visible.Count ? row : null;
        }
    }

    private void ToggleCore(Core core)
    {
        if (!checkedIds.Remove(core.id))
        {
            checkedIds.Add(core.id);
        }

        MarkDirty();
        table.SetNeedsDraw();
    }

    private void SetAllVisible(bool enabled)
    {
        foreach (var core in visible)
        {
            if (enabled)
            {
                checkedIds.Add(core.id);
            }
            else
            {
                checkedIds.Remove(core.id);
            }
        }

        MarkDirty();
        table.SetNeedsDraw();
    }

    private void Save()
    {
        var settings = ServiceHelper.SettingsService;
        int enabled = 0;
        int disabled = 0;

        foreach (var core in cores)
        {
            if (checkedIds.Contains(core.id))
            {
                settings.EnableCore(core.id);
                enabled++;
            }
            else
            {
                settings.DisableCore(core.id);
                disabled++;
            }
        }

        settings.Config.download_new_cores = (newCoreSelector.Value ?? 0) switch
        {
            1 => "yes",
            2 => "no",
            _ => "ask"
        };

        settings.Save();
        ServiceHelper.ReloadSettings();
        context.CoreUpdater.ReloadSettings();

        dirty = false;
        UpdateHint();

        TuiApp.PostStatus($"Core selection saved: {enabled} enabled, {disabled} disabled.");
    }

    private void FocusTableSoon() => TuiHost.Invoke(() => table.SetFocus());

    private static ColumnStyle FixedColumn(int width, Alignment alignment = Alignment.Start) =>
        new() { MinWidth = width, MaxWidth = width, MinAcceptableWidth = width, Alignment = alignment };

    private static string CategoryKey(Core c) =>
        string.IsNullOrEmpty(c.platform?.category) ? "(none)" : c.platform.category;

    private static string PlatformKey(Core c) =>
        string.IsNullOrEmpty(c.platform_id) ? "(none)" : c.platform_id;

    private static List<(string Key, string Display)> BuildChoices(IReadOnlyList<string> keys, Func<string, string> display) =>
        keys
            .Where(k => !string.IsNullOrEmpty(k))
            .Distinct()
            .Select(k => (Key: k, Display: display(k)))
            .OrderBy(x => x.Display, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static List<string> ToChoiceLabels(List<(string Key, string Display)> choices)
    {
        var labels = new List<string> { Pad("(All)") };
        labels.AddRange(choices.Select(c => Pad(c.Display)));
        return labels;
    }

    private static string Pad(string display) => $" {display} ";
}
