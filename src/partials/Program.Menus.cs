using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Settings;

namespace Pannella;

internal partial class Program
{
    private static int DisplayMenuNew()
    {
        Console.Clear();

        string[] menuItems =
        {
            "Update All",
            "Update Firmware",
            "Download Required Assets",
            "Select Cores",
            "Download Platform Image Packs",
            "Generate Instance JSON Files",
            "Generate Game and Watch ROMS",
            "Enable All Display Modes",
            "Backup Saves Directory",
            "Settings",
            "Exit"
        };

        Random random = new Random();
        int i = random.Next(0, WELCOME_MESSAGES.Length);
        string welcome = WELCOME_MESSAGES[i];
        int choice = 0;

        var menu = new ConsoleMenu()
            .Configure(config =>
            {
                config.Selector = "=>";
                //config.EnableFilter = true;
                config.Title = $"{welcome}\r\n{GetRandomSponsorLinks()}\r\n";
                config.EnableWriteTitle = true;
                //config.EnableBreadcrumb = true;
                config.WriteHeaderAction = () => Console.WriteLine("Choose your destiny:");
                config.SelectedItemBackgroundColor = Console.ForegroundColor;
                config.SelectedItemForegroundColor = Console.BackgroundColor;
            });

        foreach (var item in menuItems)
        {
            menu.Add(item, thisMenu =>
            {
                choice = thisMenu.CurrentItem.Index;
                thisMenu.CloseMenu();
            });
        }

        menu.Show();

        return choice;
    }

        private static void AskAboutNewCores(bool force = false)
    {
        while (GlobalHelper.SettingsManager.GetConfig().download_new_cores == null || force)
        {
            force = false;

            Console.WriteLine("Would you like to, by default, install new cores? [Y]es, [N]o, [A]sk for each:");

            ConsoleKey response = Console.ReadKey(false).Key;

            GlobalHelper.SettingsManager.GetConfig().download_new_cores = response switch
            {
                ConsoleKey.Y => "yes",
                ConsoleKey.N => "no",
                ConsoleKey.A => "ask",
                _ => null
            };
        }
    }

    private static async Task RunCoreSelector(List<Core> cores, string message = "Select your cores.")
    {
        if (GlobalHelper.SettingsManager.GetConfig().download_new_cores?.ToLowerInvariant() == "yes")
        {
            foreach (Core core in cores)
            {
                GlobalHelper.SettingsManager.EnableCore(core.identifier);
            }
        }
        else
        {
            const int pageSize = 15;
            var offset = 0;
            bool more = true;

            while (more)
            {
                var menu = new ConsoleMenu()
                    .Configure(config =>
                    {
                        config.Selector = "=>";
                        config.EnableWriteTitle = false;
                        config.WriteHeaderAction = () => Console.WriteLine($"{message} Use enter to check/uncheck your choices.");
                        config.SelectedItemBackgroundColor = Console.ForegroundColor;
                        config.SelectedItemForegroundColor = Console.BackgroundColor;
                        //config.WriteItemAction = item => Console.Write("{1}", item.Index, item.Name);
                        config.WriteItemAction = item => Console.Write("{0}", item.Name);
                    });
                var current = -1;

                if ((offset + pageSize) <= cores.Count)
                {
                    menu.Add("Next Page", thisMenu =>
                    {
                        offset += pageSize;
                        thisMenu.CloseMenu();
                    });
                }

                foreach (Core core in cores)
                {
                    current++;

                    if ((current <= (offset + pageSize)) && (current >= offset))
                    {
                        var coreSettings = GlobalHelper.SettingsManager.GetCoreSettings(core.identifier);
                        var selected = !coreSettings.skip;
                        var name = core.identifier;

                        if (core.requires_license)
                        {
                            name += " (Requires beta access)";
                        }

                        var title = MenuItemName(name, selected);

                        menu.Add(title, thisMenu =>
                        {
                            selected = !selected;

                            if (!selected)
                            {
                                GlobalHelper.SettingsManager.DisableCore(core.identifier);
                            }
                            else
                            {
                                GlobalHelper.SettingsManager.EnableCore(core.identifier);
                            }

                            thisMenu.CurrentItem.Name = MenuItemName(core.identifier, selected);
                        });
                    }
                }

                if ((offset + pageSize) <= cores.Count)
                {
                    menu.Add("Next Page", thisMenu =>
                    {
                        offset += pageSize;
                        thisMenu.CloseMenu();
                    });
                }

                menu.Add("Save Choices", thisMenu =>
                {
                    thisMenu.CloseMenu();
                    more = false;
                });

                menu.Show();
            }
        }

        GlobalHelper.SettingsManager.GetConfig().core_selector = false;
        GlobalHelper.SettingsManager.SaveSettings();
    }

    private static void SettingsMenu()
    {
        Console.Clear();

        var menuItems = new Dictionary<string, string>
        {
            { "download_firmware", "Download Firmware Updates during 'Update All'" },
            { "download_assets", "Download Missing Assets (ROMs and BIOS Files) during 'Update All'" },
            { "build_instance_jsons", "Build game JSON files for supported cores during 'Update All'" },
            { "delete_skipped_cores", "Delete untracked cores during 'Update All'" },
            { "fix_jt_names", "Automatically rename Jotego cores during 'Update All" },
            { "crc_check", "Use CRC check when checking ROMs and BIOS files" },
            { "preserve_platforms_folder", "Preserve 'Platforms' folder during 'Update All'" },
            { "skip_alternative_assets", "Skip alternative roms when downloading assets" },
            { "backup_saves", "Compress and backup Saves directory during 'Update All'" },
            { "use_custom_archive", "Use custom asset archive" }
        };

        var type = typeof(Config);
        var menu = new ConsoleMenu()
            .Configure(config =>
            {
                config.Selector = "=>";
                config.EnableWriteTitle = false;
                config.WriteHeaderAction = () => Console.WriteLine("Settings. Use enter to check/uncheck your choices.");
                config.SelectedItemBackgroundColor = Console.ForegroundColor;
                config.SelectedItemForegroundColor = Console.BackgroundColor;
                //config.WriteItemAction = item => Console.Write("{1}", item.Index, item.Name);
                config.WriteItemAction = item => Console.Write("{0}", item.Name);
            });

        foreach (var (name, text) in menuItems)
        {
            var property = type.GetProperty(name);
            var value = (bool)property.GetValue(GlobalHelper.SettingsManager.GetConfig());
            var title = MenuItemName(text, value);

            menu.Add(title, thisMenu =>
            {
                value = !value;
                property.SetValue(GlobalHelper.SettingsManager.GetConfig(), value);
                thisMenu.CurrentItem.Name = MenuItemName(text, value);
            });
        }

        menu.Add("Save", thisMenu => { thisMenu.CloseMenu(); });

        menu.Show();

        GlobalHelper.SettingsManager.SaveSettings();
    }

    private static string MenuItemName(string title, bool value)
    {
        return $"[{ (value ? "x" : " ") }] {title}";
    }
}
