using System.Collections.Generic;
using System.Linq;
using Terminal.Gui.Configuration;

namespace Pannella.TUI;

/// <summary>
/// Centralizes Terminal.Gui theme handling: loads the built-in (library) themes and applies one by
/// name. Used at startup (from the saved <c>tui_theme</c> setting) and by the in-app theme changer.
/// Only the library/hard-coded config locations are enabled, so themes are deterministic and don't
/// vary with per-machine user/env config files.
/// </summary>
public static class TuiTheme
{
    public const string DefaultTheme = "Dark";

    private static bool enabled;

    /// <summary>Loads the built-in themes (idempotent).</summary>
    public static void EnsureEnabled()
    {
        if (enabled)
        {
            return;
        }

        ConfigurationManager.Enable(ConfigLocations.HardCoded | ConfigLocations.LibraryResources);
        enabled = true;
    }

    public static IReadOnlyList<string> AvailableThemes()
    {
        EnsureEnabled();
        return ThemeManager.GetThemeNames().ToList();
    }

    /// <summary>
    /// Applies the named theme, falling back to the default (then the current) if it isn't known.
    /// Returns the name actually applied. Does not redraw — callers changing the theme on a running
    /// UI should follow with <see cref="TuiHost.Refresh"/>.
    /// </summary>
    public static string Apply(string name)
    {
        EnsureEnabled();

        var themes = ThemeManager.GetThemeNames().ToList();

        string chosen = themes.Contains(name) ? name
            : themes.Contains(DefaultTheme) ? DefaultTheme
            : ThemeManager.GetCurrentThemeName();

        ThemeManager.Theme = chosen;
        ConfigurationManager.Apply();

        return chosen;
    }
}
