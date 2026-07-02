using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Base for tabs whose content is a menu of actions. Renders the actions as a single-column
/// ListView (Enter runs the highlighted action) instead of spaced-out buttons. This keeps
/// arrow-key navigation INSIDE the tab — a ListView captures Up/Down, so focus never escapes
/// back to the tab strip the way it did with loose buttons — and stays compact (one row per
/// action) so every action is reachable even when the status pane is expanded. It scrolls if the
/// list ever outgrows the area.
/// </summary>
public abstract class ActionMenuTab : FrameView
{
    private readonly List<Action> actions = new();
    private readonly ObservableCollection<string> labels = new();
    private readonly MenuListView list;

    protected TuiContext Context { get; }

    /// <summary>Number of registered actions — used by the shell's global item-key accelerator.</summary>
    public int ItemCount => actions.Count;

    protected ActionMenuTab(TuiContext context, string title)
    {
        Context = context;
        Title = title;

        var hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "↑/↓ move · Enter or an item's [key] runs it"
        };

        list = new MenuListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        list.SetSource(labels);

        // Click / Enter run the highlighted item; the 0-9/G-Z key accelerators are driven by the
        // shell's global handler (via RunItem), not here, so they don't depend on list focus.
        list.OnActivate(RunItem);

        Add(hint);
        Add(list);
    }

    /// <summary>Runs the item at <paramref name="index"/> (no-op if out of range), highlighting it
    /// first. Public so the shell's global item-key accelerator can invoke it without list focus.</summary>
    public void RunItem(int index)
    {
        if (index < 0 || index >= actions.Count)
        {
            return;
        }

        list.SetSelection(index, false);
        actions[index]();
    }

    /// <summary>Registers a menu entry; the label appears in the list, the action runs on Enter.</summary>
    protected void AddAction(string label, Action action)
    {
        // labels.Count (before the Add) is this item's index → its accelerator prefix.
        labels.Add(TuiAccelerators.FormatItem(labels.Count, label));
        actions.Add(action);
    }

    /// <summary>Clears all entries — for tabs (e.g. Plugins) that rebuild their list dynamically.</summary>
    protected void ClearActions()
    {
        labels.Clear();
        actions.Clear();
    }
}
