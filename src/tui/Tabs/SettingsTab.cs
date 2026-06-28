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
/// Settings tab. Every bool Config property carrying a [Description] becomes a checkable row
/// (reusing the classic SettingsMenu reflection). Space toggles a row; changes are committed by the
/// Save button (click or Tab+Enter) or by pressing Enter on the list (SettingsService.Save + reload
/// both services).
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
            Text = "↑/↓ move · Space toggles · Enter or the Save button commits"
        };

        list = new MenuListView
        {
            X = 0,
            Y = 1,
            Width = Dim.Fill(),
            Height = Dim.Fill(1), // leave the bottom row for the Save button
            ShowMarks = true,
            MarkMultiple = true
        };

        var saveButton = new Button
        {
            X = Pos.Center(),
            Y = Pos.AnchorEnd(1),
            Text = "_Save"
        };

        saveButton.Accepting += (_, e) =>
        {
            e.Handled = true;
            SaveSettings();
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

        // Enter commits all toggles.
        list.Accepting += (_, e) =>
        {
            e.Handled = true;
            SaveSettings();
        };

        Add(hint);
        Add(list);
        Add(saveButton);
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
        ServiceHelper.ReloadSettings();
        context.CoreUpdater.ReloadSettings();

        TuiApp.PostStatus("Settings saved.");
    }
}
