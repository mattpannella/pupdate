using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using Pannella.Helpers;
using Pannella.Models.Settings;
using Terminal.Gui.ViewBase;
using Terminal.Gui.Views;

namespace Pannella.TUI;

/// <summary>
/// Settings tab. Reuses the same reflection the classic SettingsMenu used — every bool Config
/// property carrying a [Description] becomes a checkable row — rendered as a marking ListView
/// (Space toggles) so it scrolls cleanly. Save writes via SettingsService and reloads both
/// services so changes take effect immediately.
/// </summary>
public sealed class SettingsTab : FrameView
{
    private readonly TuiContext context;
    private readonly List<PropertyInfo> properties = new();
    private readonly ListView list;

    public SettingsTab(TuiContext context)
    {
        this.context = context;
        Title = "Settings";

        var hint = new Label
        {
            X = 0,
            Y = 0,
            Width = Dim.Fill(),
            Text = "↑/↓ move · Space toggles · then Save"
        };

        list = new ListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(2),
            CanFocus = true,
            ShowMarks = true,
            MarkMultiple = true
        };

        var config = ServiceHelper.SettingsService.Config;
        var labels = new ObservableCollection<string>();

        var entries =
            from property in typeof(Config).GetProperties()
            let attribute = property.GetCustomAttributes(typeof(DescriptionAttribute), true)
            where attribute.Length == 1
            select (property, ((DescriptionAttribute)attribute[0]).Description);

        foreach (var (property, description) in entries)
        {
            properties.Add(property);
            labels.Add(description);
        }

        list.SetSource(labels);

        for (int i = 0; i < properties.Count; i++)
        {
            list.Source.SetMark(i, (bool)(properties[i].GetValue(config) ?? false));
        }

        list.VerticalScrollBar.VisibilityMode = ScrollBarVisibilityMode.Auto;

        var save = new Button
        {
            X = 1,
            Y = Pos.AnchorEnd(1),
            Text = "_Save settings"
        };

        save.Accepting += (_, e) =>
        {
            e.Handled = true;
            SaveSettings();
        };

        Add(hint);
        Add(list);
        Add(save);
    }

    private void SaveSettings()
    {
        var config = ServiceHelper.SettingsService.Config;
        var marked = new HashSet<int>(list.GetAllMarkedItems());

        for (int i = 0; i < properties.Count; i++)
        {
            properties[i].SetValue(config, marked.Contains(i));
        }

        ServiceHelper.SettingsService.Save();

        // Reload BOTH so the new settings take effect immediately (issue #299); this also
        // re-attaches the TUI status sink to the rebuilt services.
        ServiceHelper.ReloadSettings();
        context.CoreUpdater.ReloadSettings();

        TuiApp.PostStatus("Settings saved.");
    }
}
