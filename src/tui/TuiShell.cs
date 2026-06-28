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
    public StatusPane StatusPane { get; }

    public TuiShell(TuiContext context)
    {
        Title = "Pupdate — openFPGA updater (Esc to quit)";

        var tabs = new Tabs
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Height = Dim.Percent(70)
        };

        // Tab labels come from each child view's Title.
        tabs.InsertTab(0, new UpdateTab(context));
        tabs.InsertTab(1, Placeholder("Cores"));
        tabs.InsertTab(2, Placeholder("Setup"));
        tabs.InsertTab(3, Placeholder("Maintenance"));
        tabs.InsertTab(4, Placeholder("Extras"));
        tabs.InsertTab(5, Placeholder("Settings"));

        StatusPane = new StatusPane
        {
            X = 0,
            Y = Pos.Bottom(tabs),
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        Add(tabs);
        Add(StatusPane);

        // Esc requests a clean stop; Application.Shutdown runs in TuiApp.Run's finally.
        KeyDown += (_, key) =>
        {
            if (key == Key.Esc)
            {
                TuiHost.RequestStop();
                key.Handled = true;
            }
        };
    }

    private static View Placeholder(string title)
    {
        var view = new FrameView
        {
            Title = title,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        view.Add(new Label
        {
            X = 1,
            Y = 1,
            Text = $"{title} — coming in a later phase."
        });

        return view;
    }
}
