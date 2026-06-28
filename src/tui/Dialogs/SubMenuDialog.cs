using System;
using System.Collections.Generic;
using System.Linq;

namespace Pannella.TUI;

/// <summary>
/// A lightweight submenu: shows a single-select popup of labeled actions and runs the chosen one.
/// Built on <see cref="SelectDialog"/> so it inherits the standard hover/click/Enter behavior.
/// Lets a flat <see cref="ActionMenuTab"/> group related actions (e.g. Display Modes, Downloads)
/// under one entry instead of listing a dozen items inline.
/// </summary>
public static class SubMenuDialog
{
    public static void Show(string title, IReadOnlyList<(string Label, Action Action)> items)
    {
        if (items == null || items.Count == 0)
        {
            return;
        }

        int? choice = SelectDialog.Show(title, "Choose an option:", items.Select(i => i.Label).ToList());

        if (choice == null)
        {
            return;
        }

        items[choice.Value].Action?.Invoke();
    }
}
