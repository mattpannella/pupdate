using System.ComponentModel;
using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.Settings;

namespace Pannella;

internal static partial class Program
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

        foreach ((string name, string text) in menuItems)
        {
            var property = type.GetProperty(name);
            var value = (bool)property!.GetValue(ServiceHelper.SettingsService.Config)!;
            var title = MenuItemName(text, value);

            menu.Add(title, thisMenu =>
            {
                value = !value;
                property.SetValue(ServiceHelper.SettingsService.Config, value);
                thisMenu.CurrentItem.Name = MenuItemName(text, value);
            });
        }

        var config = ServiceHelper.SettingsService.Config;
        string AiThresholdLabel() => $"Set AI Filter Threshold (current: {config.ai_core_threshold}%)";

        menu.Add(AiThresholdLabel(), thisMenu =>
        {
            Console.WriteLine($"Current AI filter threshold: {config.ai_core_threshold}%");
            Console.WriteLine("Cores with an AI score over this percentage are hidden (0-100).");

            string input = PromptForInput();

            if (int.TryParse(input?.Trim(), out int pct))
                config.ai_core_threshold = Math.Clamp(pct, 0, 100);

            thisMenu.CurrentItem.Name = AiThresholdLabel();
        });

        menu.Add("Save", thisMenu => { thisMenu.CloseMenu(); });

        menu.Show();

        ServiceHelper.SettingsService.Save();
    }
}
