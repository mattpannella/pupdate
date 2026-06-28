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

    protected TuiContext Context { get; }

    protected ActionMenuTab(TuiContext context, string title)
    {
        Context = context;
        Title = title;

        var hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "↑/↓ move · Enter runs the highlighted item"
        };

        var list = new MenuListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill()
        };

        list.SetSource(labels);

        // Hover + scrollbar come from MenuListView; single click / Enter runs the item.
        list.OnActivate(Run);

        Add(hint);
        Add(list);
    }

    private void Run(int index)
    {
        if (index >= 0 && index < actions.Count)
        {
            actions[index]();
        }
    }

    /// <summary>Registers a menu entry; the label appears in the list, the action runs on Enter.</summary>
    protected void AddAction(string label, Action action)
    {
        // Leading marker so rows read as actionable menu items rather than plain text.
        labels.Add($"▸ {label}");
        actions.Add(action);
    }

    /// <summary>Clears all entries — for tabs (e.g. Plugins) that rebuild their list dynamically.</summary>
    protected void ClearActions()
    {
        labels.Clear();
        actions.Clear();
    }
}
