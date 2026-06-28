using System.Collections.Generic;
using System.Linq;
using Pannella.Models.DisplayModes;
using Pannella.Services;

namespace Pannella.TUI;

/// <summary>
/// Display-mode selection, built on the shared <see cref="ChecklistDialog"/> (enforces the
/// 16-max cap). Returns the selected display-mode values, or null if cancelled.
/// </summary>
public static class DisplayModeSelectorDialog
{
    public static List<string> Show(IReadOnlyList<DisplayMode> modes)
    {
        var marked = ChecklistDialog.Show(
            "Display Modes",
            $"Select up to {CoresService.DISPLAY_MODES_MAX}",
            modes.Select(m => m.description).ToList(),
            _ => false,
            "Apply",
            maxSelected: CoresService.DISPLAY_MODES_MAX);

        return marked?.Select(i => modes[i].value).ToList();
    }
}
