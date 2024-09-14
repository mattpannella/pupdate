using System.ComponentModel;
using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.Settings;

namespace Pannella;

internal partial class Program
{
    private static void SettingsMenu()
    {
        Console.Clear();

        var type = typeof(Config);
        var menuItems =
            from property in type.GetProperties()
            let attribute = property.GetCustomAttributes(typeof(DescriptionAttribute), true)
            where attribute.Length == 1
            select (property.Name, ((DescriptionAttribute)attribute[0]).Description);
        var menu = new ConsoleMenu()
            .Configure(config =>
            {
                config.Selector = "=>";
                config.EnableWriteTitle = false;
                config.WriteHeaderAction = () => Console.WriteLine("Settings. Use enter to check/uncheck your choices.");
                config.SelectedItemBackgroundColor = Console.ForegroundColor;
                config.SelectedItemForegroundColor = Console.BackgroundColor;
                config.WriteItemAction = item => Console.Write("{0}", item.Name);
            });

        foreach (var (name, text) in menuItems)
        {
            var property = type.GetProperty(name);
            var value = (bool)property!.GetValue(ServiceHelper.SettingsService.GetConfig())!;
            var title = MenuItemName(text, value);

            menu.Add(title, thisMenu =>
            {
                value = !value;
                property.SetValue(ServiceHelper.SettingsService.GetConfig(), value);
                thisMenu.CurrentItem.Name = MenuItemName(text, value);
            });
        }

        menu.Add("Save", thisMenu => { thisMenu.CloseMenu(); });

        menu.Show();

        ServiceHelper.SettingsService.Save();
    }
}
