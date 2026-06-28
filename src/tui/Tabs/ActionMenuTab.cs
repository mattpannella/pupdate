using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Terminal.Gui.Input;
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

        // Activation (hover + scrollbar come from MenuListView): single click runs the item
        // (deferred so the click first moves the selection to the clicked row), Enter runs the
        // highlighted item from the keyboard.
        list.MouseEvent += (_, mouse) =>
        {
            if (mouse.IsSingleClicked && mouse.Position is { } position)
            {
                // Only activate when the click lands on an actual item row — not the empty space
                // below the list, where the selection still points at the last-hovered item.
                int row = list.Viewport.Y + position.Y;

                if (row >= 0 && row < labels.Count)
                {
                    TuiHost.Invoke(() => Run(row));
                }
            }
        };

        list.KeyDown += (_, key) =>
        {
            if (key == Key.Enter)
            {
                Run(list.SelectedItem);
                key.Handled = true;
            }
        };

        Add(hint);
        Add(list);
    }

    private void Run(int? index)
    {
        if (index.HasValue && index.Value >= 0 && index.Value < actions.Count)
        {
            actions[index.Value]();
        }
    }

    /// <summary>Registers a menu entry; the label appears in the list, the action runs on Enter.</summary>
    protected void AddAction(string label, Action action)
    {
        // Leading marker so rows read as actionable menu items rather than plain text.
        labels.Add($"▸ {label}");
        actions.Add(action);
    }
}
