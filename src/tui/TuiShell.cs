using System;
using Terminal.Gui.Input;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// The root full-screen window: a tab strip across the top (one section per tab) over the
/// persistent <see cref="StatusPane"/>.
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
    private readonly PlatformLimitBar platformLimit;
    private readonly View[] orderedTabs;
    private readonly SettingsTab settingsTab;
    private bool statusExpanded;

    public TuiShell(TuiContext context)
    {
        Title = "pupdate (Beta)";

        // Pinned welcome banner - the ASCII art the classic menu prints across the top.
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

        platformLimit = new PlatformLimitBar { X = 0, Y = Pos.Bottom(header) };

        // Tabs + status stay direct children of the window (so the Tabs control keeps focus and
        // arrow/click navigation), anchored just below the banner.
        tabs = new Tabs
        {
            X = 0,
            Y = Pos.Bottom(platformLimit),
            Width = Dim.Fill(),
            Height = Dim.Percent(TabsHeightCollapsed)
        };

        // Tab labels come from each child view's Title. Prefix each with its single-key switch
        // shortcut ("[A] Main", "[B] Setup", …) BEFORE inserting, so the header shows the letter
        // regardless of whether Tabs snapshots the title at insert-time or re-reads it on draw.
        var pluginsTab = new PluginsTab(context);
        var coresTab = new CoresTab(context);
        settingsTab = new SettingsTab(context);
        orderedTabs = new View[] { new UpdatesTab(context), coresTab, new PocketSetupTab(context),
            new MaintenanceTab(context), new ExtrasTab(context), pluginsTab, settingsTab };
        View previousTab = orderedTabs[0];

        for (int i = 0; i < orderedTabs.Length; i++)
        {
            orderedTabs[i].Title = $" {TuiAccelerators.FormatTab(i, orderedTabs[i].Title)} ";
            tabs.InsertTab(i, orderedTabs[i]);
        }

        // Re-discover plugins each time the Plugins tab is opened (catches plugins added or
        // removed outside this session).
        tabs.ValueChanged += (_, _) =>
        {
            if (previousTab == settingsTab)
            {
                settingsTab.CommitIfDirty();
            }

            previousTab = tabs.Value;

            if (tabs.Value == pluginsTab)
            {
                pluginsTab.Refresh();
            }
            else if (tabs.Value == coresTab)
            {
                coresTab.Activated();
            }
            else if (tabs.Value == settingsTab)
            {
                settingsTab.Refresh();
            }

            platformLimit.Refresh();
        };

        StatusPane = new StatusPane
        {
            X = 0,
            Y = Pos.Bottom(tabs),
            Width = Dim.Fill(),
            Height = Dim.Fill(1)
        };

        // Bottom key-hint bar. Display + click only: BindKeyToApplication stays false so these
        // never compete with the KeyDown handling below (an app-bound Esc would also fire around
        // modals). Clicking an entry invokes the same method as its key.
        var statusBar = new StatusBar(new[]
        {
            new Shortcut(Key.Esc, "Quit", Quit, null),
            new Shortcut(Key.F6, "Status pane", ToggleStatusPane, null),
            new Shortcut(Key.F9, "Theme", PickTheme, null),
            new Shortcut(Key.Empty, "←/→ or A–G tabs · 0–9/H–Z items", null, null)
        })
        {
            CanFocus = false
        };

        Add(header);
        Add(platformLimit);
        Add(tabs);
        Add(StatusPane);
        Add(statusBar);

        platformLimit.Refresh();

        // Auto-expand the status pane when an operation starts so the live log/summary is easy to
        // follow. We deliberately do NOT collapse on completion - that would scroll the summary out
        // of view - so it stays expanded until the user presses F6. StatusPane is anchored to
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
                Quit();
                key.Handled = true;
            }
            else if (key == Key.F6)
            {
                ToggleStatusPane();
                key.Handled = true;
            }
            else if (key == Key.F9)
            {
                PickTheme();
                key.Handled = true;
            }
        };

        // Accelerators hook the app-wide key stream (not the bubble-up KeyDown above) so they pre-empt
        // the focused view's own key handling (e.g. ListView type-ahead) and work regardless of focus.
        TuiHost.AddGlobalKeyDown(OnGlobalKeyDown);
    }

    // Esc requests a clean stop; Application.Shutdown runs in TuiApp.Run's finally.
    private void Quit()
    {
        settingsTab.CommitIfDirty();
        TuiHost.RequestStop();
    }

    private void ToggleStatusPane()
    {
        statusExpanded = !statusExpanded;
        ApplyLayout();
    }

    // App-wide theme chooser (deliberately not in a tab - it's a pupdate-UI setting).
    private static void PickTheme() => ThemePickerDialog.Show();

    private void OnGlobalKeyDown(object sender, Key key)
    {
        // Stand down while a modal owns the screen.
        if (!TuiHost.IsTopRunnable(this) || key.IsCtrl || key.IsAlt)
        {
            return;
        }

        if (key == Key.CursorUp || key == Key.CursorDown)
        {
            GuardVerticalKey();
            return;
        }

        if (key == Key.CursorLeft || key == Key.CursorRight)
        {
            SwitchTab(key == Key.CursorRight ? 1 : -1, key);
            return;
        }

        // Accelerators are single characters; ignore anything else.
        if (key.AsRune.Value == 0)
        {
            return;
        }

        char c = (char)key.AsRune.Value;
        int tabIndex = TuiAccelerators.TabIndex(c);

        if (tabIndex >= 0 && tabIndex < orderedTabs.Length)
        {
            tabs.Value = orderedTabs[tabIndex];
            orderedTabs[tabIndex].SetFocus(); // so arrow keys land on the tab's list after a jump
            key.Handled = true;
            return;
        }

        // 0-9/G-Z run the Nth item on the active action tab. Extras/Settings aren't ActionMenuTabs, so
        // their keys fall through to their own list behavior.
        int itemIndex = TuiAccelerators.ItemIndex(c);

        if (itemIndex >= 0 && tabs.Value is ActionMenuTab activeTab && itemIndex < activeTab.ItemCount)
        {
            key.Handled = true;
            // Defer so the key event unwinds before the action runs (it may open a modal / long op).
            TuiHost.Invoke(() => activeTab.RunItem(itemIndex));
        }
    }

    // Up/Down belong to the focused list, never to the tab strip. Views that don't consume them let
    // the key bubble into focus navigation, which walks out of the tab - so snap the selection back
    // if that happened (issue #517). Runs on the next loop iteration, after the key has been
    // dispatched to the focused view.
    private void GuardVerticalKey()
    {
        var current = tabs.Value;

        if (current == null)
        {
            return;
        }

        TuiHost.Invoke(() =>
        {
            if (tabs.Value != current)
            {
                tabs.Value = current;
                current.SetFocus();
            }
        });
    }

    // Left/Right move between tabs. Only views whose horizontal keys carry real meaning opt out: a
    // text caret and the Cores tab's horizontal option selector. Notably NOT the cores TableView -
    // it's FullRowSelect with row-level actions, so its column navigation just ate five Right
    // presses before the tab would move (issue #517).
    private void SwitchTab(int direction, Key key)
    {
        if (TuiHost.Focused is TextField or TextView or OptionSelector)
        {
            return;
        }

        int index = Array.IndexOf(orderedTabs, tabs.Value);

        if (index < 0)
        {
            return;
        }

        int target = Math.Clamp(index + direction, 0, orderedTabs.Length - 1);
        key.Handled = true;

        if (target != index)
        {
            tabs.Value = orderedTabs[target];
            orderedTabs[target].SetFocus();
        }
    }

    private void OnBusyChanged(bool busy)
    {
        StatusPane.SetBusy(busy);

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
            // Op finished (on the UI thread via TuiHost.Invoke): restore focus to the active tab for
            // arrow-key nav. Guarded so we never pull focus from a modal that's still open.
            tabs.Value?.SetFocus();
            platformLimit.Refresh();
        }
    }

    private void ApplyLayout()
    {
        tabs.Height = Dim.Percent(statusExpanded ? TabsHeightExpanded : TabsHeightCollapsed);
        StatusPane.SetExpanded(statusExpanded);
        SetNeedsLayout();
    }
}
