using Pannella.Helpers;

namespace Pannella.TUI;

/// <summary>
/// App-wide theme chooser, opened by a global hotkey (not tied to any tab, since the theme is a
/// pupdate-UI concern rather than Pocket setup). Lists the built-in themes, applies the pick live,
/// and persists it to the <c>tui_theme</c> setting.
/// </summary>
public static class ThemePickerDialog
{
    public static void Show()
    {
        var themes = TuiTheme.AvailableThemes();
        string current = ServiceHelper.SettingsService.Config.tui_theme;

        int? choice = SelectDialog.Show("Theme", $"Current: {current}. Select a theme:", themes);

        if (choice == null)
        {
            return;
        }

        string applied = TuiTheme.Apply(themes[choice.Value]);
        TuiHost.Refresh();

        ServiceHelper.SettingsService.Config.tui_theme = applied;
        ServiceHelper.SettingsService.Save();
        TuiApp.PostStatus($"Theme set to '{applied}'.");
    }
}
