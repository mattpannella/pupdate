using System.ComponentModel;
using System.Reflection;
using System.Security.Cryptography;
using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Settings;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private enum MainMenuItems
    {
        None = 0,
        [Description("Update All")]
        UpdateAll = 1,
        [Description("Update Firmware")]
        UpdateFirmware,
        [Description("Download Required Assets")]
        DownloadRequiredAssets,
        [Description("Select Cores")]
        SelectCores,
        [Description("Reinstall Cores")]
        ReinstallCores,
        [Description("Uninstall Cores")]
        UninstallCores,
        [Description("Download Platform Image Packs")]
        DownloadPlatformImagePacks,
        [Description("Generate Instance JSON Files")]
        GenerateInstanceJsonFiles,
        [Description("Generate Game and Watch ROMS")]
        GenerateGameAndWatchRoms,
        [Description("Enable All Display Modes")]
        EnableAllDisplayModes,
        [Description("Backup Saves Directory")]
        BackupSavesDirectory,
        [Description("Settings")]
        Settings,
        [Description("Exit")]
        Exit
    }

    private static void DisplayMenuNew(string path, PocketCoreUpdater coreUpdater)
    {
        Console.Clear();

        Random random = new Random();
        int i = random.Next(0, WELCOME_MESSAGES.Length);
        string welcome = WELCOME_MESSAGES[i];
        MainMenuItems choice = MainMenuItems.None;

        var menuConfig = new MenuConfig
        {
            Selector = "=>",
            Title = $"{welcome}\r\n{GetRandomSponsorLinks()}\r\n",
            EnableWriteTitle = true,
            WriteHeaderAction = () => Console.WriteLine("Choose your destiny:"),
            SelectedItemBackgroundColor = Console.ForegroundColor,
            SelectedItemForegroundColor = Console.BackgroundColor,
        };

        var pocketSetupMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Download Platform Image Packs", async m =>
            {
                await ImagePackSelector(path);
            })
            .Add("Generate Instance JSON Files (PC Engine CD)", m =>
            {
                RunInstanceGenerator(coreUpdater);
                Pause();
            })
            .Add("Generate Game & Watch ROMs", async m =>
            {
                await BuildGameAndWatchRoms(path);
                Pause();
            })
            .Add("Enable All Display Modes", m =>
            {
                coreUpdater.ForceDisplayModes();
                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        var pocketMaintenanceMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Reinstall Cores", async m =>
            {
                var results = ShowCoresMenu(
                    GlobalHelper.InstalledCores,
                    "Which cores would you like to reinstall?",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    await coreUpdater.RunUpdates(item.Key, true);
                }

                Pause();
            })
            .Add("Uninstall Cores", m =>
            {
                var results = ShowCoresMenu(
                    GlobalHelper.InstalledCores,
                    "Which cores would you like to uninstall?",
                    false);

                bool nuke = AskAboutCoreSpecificAssets();

                foreach (var item in results.Where(x => x.Value))
                {
                    coreUpdater.DeleteCore(GlobalHelper.GetCore(item.Key), true, nuke);
                }

                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        var menu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Update All", async m =>
            {
                Console.WriteLine("Starting update process...");
                await coreUpdater.RunUpdates();
                Pause();
            })
            .Add("Update Firmware", async m =>
            {
                await coreUpdater.UpdateFirmware();
                Pause();
            })
            .Add("Select Cores", m =>
            {
                AskAboutNewCores(true);
                RunCoreSelector(GlobalHelper.Cores);
                // Is reloading the settings file necessary?
                GlobalHelper.ReloadSettings();
            })
            .Add("Download Assets", async m =>
            {
                Console.WriteLine("Checking for required files...");
                await coreUpdater.RunAssetDownloader();
                Pause();
            })
            .Add("Backup Saves", m =>
            {
                AssetsService.BackupSaves(path, GlobalHelper.SettingsManager.GetConfig().backup_saves_location);
                Pause();
            })
            .Add("Pocket Setup", pocketSetupMenu.Show)
            .Add("Pocket Maintenance", pocketMaintenanceMenu.Show)
            .Add("Settings", m =>
            {
                SettingsMenu();

                coreUpdater.DeleteSkippedCores(GlobalHelper.SettingsManager.GetConfig().delete_skipped_cores);
                coreUpdater.DownloadFirmware(GlobalHelper.SettingsManager.GetConfig().download_firmware);
                coreUpdater.DownloadAssets(GlobalHelper.SettingsManager.GetConfig().download_assets);
                coreUpdater.RenameJotegoCores(GlobalHelper.SettingsManager.GetConfig().fix_jt_names);
                coreUpdater.BackupSaves(GlobalHelper.SettingsManager.GetConfig().backup_saves,
                    GlobalHelper.SettingsManager.GetConfig().backup_saves_location);
                // Is reloading the settings file necessary?
                GlobalHelper.ReloadSettings();
            })
            .Add("Exit", ConsoleMenu.Close);

        menu.Show();
    }

    private static MainMenuItems DisplayMenu()
    {
        Console.Clear();

        Random random = new Random();
        int i = random.Next(0, WELCOME_MESSAGES.Length);
        string welcome = WELCOME_MESSAGES[i];
        MainMenuItems choice = MainMenuItems.None;

        var menu = new ConsoleMenu()
            .Configure(config =>
            {
                config.Selector = "=>";
                config.Title = $"{welcome}\r\n{GetRandomSponsorLinks()}\r\n";
                config.EnableWriteTitle = true;
                config.WriteHeaderAction = () => Console.WriteLine("Choose your destiny:");
                config.SelectedItemBackgroundColor = Console.ForegroundColor;
                config.SelectedItemForegroundColor = Console.BackgroundColor;
            });

        foreach (var item in Enum.GetValues<MainMenuItems>())
        {
            if (item == MainMenuItems.None)
                continue;

            FieldInfo fi = item.GetType().GetField(item.ToString());
            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi!.GetCustomAttributes(
                typeof(DescriptionAttribute), false);
            var itemDescription = attributes.Length > 0 ? attributes[0].Description : item.ToString();

            menu.Add(itemDescription, thisMenu =>
            {
                choice = item;
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

            ConsoleKey response = Console.ReadKey(true).Key;

            GlobalHelper.SettingsManager.GetConfig().download_new_cores = response switch
            {
                ConsoleKey.Y => "yes",
                ConsoleKey.N => "no",
                ConsoleKey.A => "ask",
                _ => null
            };
        }
    }

    private static bool AskAboutCoreSpecificAssets()
    {
        Console.WriteLine("Would you like to remove the core specific assets for the selected cores? [Y]es, [N]o");

        bool? result = null;

        while (result == null)
        {
            result = Console.ReadKey(true).Key switch
            {
                ConsoleKey.Y => true,
                ConsoleKey.N => false,
                _ => null
            };
        }

        return result.Value;
    }

    private static Dictionary<string, bool> ShowCoresMenu(List<Core> cores, string message, bool isCoreSelection)
    {
        const int pageSize = 15;
        var offset = 0;
        bool more = true;
        var results = new Dictionary<string, bool>();

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
                    var selected = isCoreSelection && !coreSettings.skip;
                    var name = core.identifier;

                    if (isCoreSelection && core.requires_license)
                    {
                        name += " (Requires beta access)";
                    }

                    var title = MenuItemName(name, selected);

                    menu.Add(title, thisMenu =>
                    {
                        selected = !selected;

                        if (results.ContainsKey(core.identifier))
                        {
                            results[core.identifier] = selected;
                        }
                        else
                        {
                            results.Add(core.identifier, selected);
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

        return results;
    }

    private static void RunCoreSelector(List<Core> cores, string message = "Select your cores.")
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
            var results = ShowCoresMenu(cores, message, true);

            foreach (var item in results)
            {
                if (item.Value)
                    GlobalHelper.SettingsManager.EnableCore(item.Key);
                else
                    GlobalHelper.SettingsManager.DisableCore(item.Key);
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
            { "download_gnw_roms", "Download Game n Watch ROMS during 'Update All'" },
            { "build_instance_jsons", "Build game JSON files for supported cores during 'Update All'" },
            { "delete_skipped_cores", "Delete untracked cores during 'Update All'" },
            { "fix_jt_names", "Automatically rename Jotego cores during 'Update All'" },
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
