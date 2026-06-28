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
    private bool statusExpanded;

    public TuiShell(TuiContext context)
    {
        Title = "pupdate - (Esc: quit)";

        tabs = new Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(TabsHeightCollapsed)
        };

        // Tab labels come from each child view's Title.
        var pluginsTab = new PluginsTab(context);

        tabs.InsertTab(0, new MainTab(context));
        tabs.InsertTab(1, new SetupTab(context));
        tabs.InsertTab(2, new MaintenanceTab(context));
        tabs.InsertTab(3, new ExtrasTab(context));
        tabs.InsertTab(4, pluginsTab);
        tabs.InsertTab(5, new SettingsTab(context));

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
        };
    }

    private void OnBusyChanged(bool busy)
    {
        // Expand on start; leave the layout untouched on completion so the summary stays visible.
        if (busy && !statusExpanded)
        {
            statusExpanded = true;
            ApplyLayout();
        }
    }

    private void ApplyLayout()
    {
        tabs.Height = Dim.Percent(statusExpanded ? TabsHeightExpanded : TabsHeightCollapsed);
        StatusPane.SetExpanded(statusExpanded);
        SetNeedsLayout();
    }
}
