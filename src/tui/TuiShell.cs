using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// The root full-screen window: a tab strip across the top (one section per tab) over the
/// persistent <see cref="StatusPane"/>. Phase 1 ships the Update tab wired end-to-end; the
/// remaining sections are placeholders filled in by later phases.
/// </summary>
public sealed class TuiShell : Window
{
    // Collapsed: tabs own most of the screen (status is a small log strip). Expanded: the status
    // pane takes over so a long log / completion summary is easy to read. An operation auto-expands
    // the pane; it then STAYS expanded (so the summary isn't lost) until the user toggles it (F6).
    private const int TabsHeightCollapsed = 70;
    private const int TabsHeightExpanded = 30;

    public StatusPane StatusPane { get; }

    private readonly Tabs tabs;
    private readonly View[] orderedTabs;
    private bool statusExpanded;

    public TuiShell(TuiContext context)
    {
        Title = "pupdate (Beta)  ·  A–F: tabs  ·  Esc: quit  ·  F6: status pane  ·  F9: theme";

        // Pinned welcome banner — the ASCII art the classic menu prints across the top.
        string banner = Program.RandomWelcomeBanner();
        var header = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = banner.Split('\n').Length,
            Text = banner,
            CanFocus = false
        };

        // Tabs + status stay direct children of the window (so the Tabs control keeps focus and
        // arrow/click navigation), anchored just below the banner.
        tabs = new Tabs
        {
            X = 0,
            Y = Pos.Bottom(header),
            Width = Dim.Fill(),
            Height = Dim.Percent(TabsHeightCollapsed)
        };

        // Tab labels come from each child view's Title. Prefix each with its single-key switch
        // shortcut ("[A] Main", "[B] Setup", …) BEFORE inserting, so the header shows the letter
        // regardless of whether Tabs snapshots the title at insert-time or re-reads it on draw.
        var pluginsTab = new PluginsTab(context);
        orderedTabs = new View[] { new MainTab(context), new SetupTab(context), new MaintenanceTab(context),
            new ExtrasTab(context), pluginsTab, new SettingsTab(context) };

        for (int i = 0; i < orderedTabs.Length; i++)
        {
            orderedTabs[i].Title = TuiAccelerators.FormatTab(i, orderedTabs[i].Title);
            tabs.InsertTab(i, orderedTabs[i]);
        }

        // Re-discover plugins each time the Plugins tab is opened (catches plugins added or
        // removed outside this session).
        tabs.ValueChanged += (_, _) =>
        {
            if (tabs.Value == pluginsTab)
            {
                pluginsTab.Refresh();
            }
        };

        StatusPane = new StatusPane
        {
            X = 0,
            Y = Pos.Bottom(tabs),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        Add(header);
        Add(tabs);
        Add(StatusPane);

        // Auto-expand the status pane when an operation starts so the live log/summary is easy to
        // follow. We deliberately do NOT collapse on completion — that would scroll the summary out
        // of view — so it stays expanded until the user presses F6. StatusPane is anchored to
        // Pos.Bottom(tabs) with Dim.Fill(), so resizing tabs re-flows it for free.
        context.BusyChanged += OnBusyChanged;
        StatusPane.ToggleRequested += () =>
        {
            statusExpanded = !statusExpanded;
            ApplyLayout();
        };

        KeyDown += (_, key) =>
        {
            if (key == Key.Esc)
            {
                // Esc requests a clean stop; Application.Shutdown runs in TuiApp.Run's finally.
                TuiHost.RequestStop();
                key.Handled = true;
            }
            else if (key == Key.F6)
            {
                statusExpanded = !statusExpanded;
                ApplyLayout();
                key.Handled = true;
            }
            else if (key == Key.F9)
            {
                // App-wide theme chooser (deliberately not in a tab — it's a pupdate-UI setting).
                ThemePickerDialog.Show();
                key.Handled = true;
            }
        };

        // Tab-jump (A–F) is a GLOBAL shortcut: it must win even when the focused view would consume
        // the letter itself (e.g. the Settings list's first-letter type-ahead), and must keep working
        // after focus drifts to the status pane during an operation. So it can't ride the bubble-up
        // KeyDown above — it hooks the app-wide key stream, which fires before the focused view.
        TuiHost.AddGlobalKeyDown(OnGlobalKeyDown);
    }

    private void OnGlobalKeyDown(object sender, Key key)
    {
        // Stand down while a modal dialog owns the screen (so its own keys/typing are untouched), and
        // ignore modified/non-character keys.
        if (!TuiHost.IsTopRunnable(this) || key.IsCtrl || key.IsAlt || key.AsRune.Value == 0)
        {
            return;
        }

        char c = (char)key.AsRune.Value;

        // Tab-jump: a plain letter (A–F) switches tabs, pre-empting the focused view.
        int tabIndex = TuiAccelerators.TabIndex(c);

        if (tabIndex >= 0 && tabIndex < orderedTabs.Length)
        {
            tabs.Value = orderedTabs[tabIndex];
            orderedTabs[tabIndex].SetFocus(); // arrow keys land on the tab's list after a jump
            key.Handled = true;
            return;
        }

        // Item accelerator (0-9/G-Z): run the matching item on the active action-list tab. Driven here
        // rather than on the list itself so it works regardless of focus — after running an item, a
        // completed operation, etc. Extras/Settings aren't ActionMenuTabs, so their keys fall through
        // to their own list behavior.
        int itemIndex = TuiAccelerators.ItemIndex(c);

        if (itemIndex >= 0 && tabs.Value is ActionMenuTab activeTab && itemIndex < activeTab.ItemCount)
        {
            key.Handled = true;
            // Defer so the key event fully unwinds before the action runs (it may open a modal or
            // start a long operation) — mirrors the deferred mouse-activation path in MenuListView.
            TuiHost.Invoke(() => activeTab.RunItem(itemIndex));
        }
    }

    private void OnBusyChanged(bool busy)
    {
        // Expand on start; leave the layout untouched on completion so the summary stays visible.
        if (busy)
        {
            if (!statusExpanded)
            {
                statusExpanded = true;
                ApplyLayout();
            }
        }
        else if (TuiHost.IsTopRunnable(this))
        {
            // Operation finished (raised on the UI thread via TuiHost.Invoke): return focus to the
            // active tab so its item-key accelerators work again without a click back into the list.
            // Only when the shell owns the screen — never steal focus from a modal that's still open.
            tabs.Value?.SetFocus();
        }
    }

    private void ApplyLayout()
    {
        tabs.Height = Dim.Percent(statusExpanded ? TabsHeightExpanded : TabsHeightCollapsed);
        StatusPane.SetExpanded(statusExpanded);
        SetNeedsLayout();
    }
}
