using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Settings;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private static void DisplayMenuNew(string path, PocketCoreUpdater coreUpdater)
    {
        Console.Clear();

        Random random = new Random();
        int i = random.Next(0, WELCOME_MESSAGES.Length);
        string welcome = WELCOME_MESSAGES[i];

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
            .Add("Download Platform Image Packs", async _ =>
            {
                await ImagePackSelector(path);
            })
            .Add("Generate Instance JSON Files (PC Engine CD)", () =>
            {
                RunInstanceGenerator(coreUpdater);
                Pause();
            })
            .Add("Generate Game & Watch ROMs", async _ =>
            {
                await BuildGameAndWatchRoms(path);
                Pause();
            })
            .Add("Enable All Display Modes", () =>
            {
                coreUpdater.ForceDisplayModes();
                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        var pocketMaintenanceMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Reinstall All Cores", async _ =>
            {
                await coreUpdater.RunUpdates(null, true);
                Pause();
            })
            .Add("Reinstall Select Cores", async _ =>
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
            .Add("Uninstall Select Cores", () =>
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

        var pocketExtrasMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Download Pocket Extras for ericlewis.DonkeyKong", async _ =>
            {
                await DownloadDonkeyKongPocketExtras(path, coreUpdater);
                Pause();
            })
            .Add("Download Pocket Extras for ericlewis.RadarScope", async _ =>
            {
                await DownloadRadarScopePocketExtras(path, coreUpdater);
                Pause();
            })
            .Add("Download Pocket Extras for jotego.jtbubl", async _ =>
            {
                await DownloadBubbleBobblePocketExtras(path, coreUpdater);
                Pause();
            })
            .Add("Download Pocket Extras for jotego.jtcps15", async _ =>
            {
                await DownloadCapcomCps15PocketExtras(path, coreUpdater);
                Pause();
            })
            .Add("Download Pocket Extras for jotego.jtcps2", async _ =>
            {
                await DownloadCapcomCps2PocketExtras(path, coreUpdater);
                Pause();
            })
            .Add("Download Pocket Extras for jotego.jtpang", async _ =>
            {
                await DownloadPangPocketExtras(path, coreUpdater);
                Pause();
            })
            .Add("Download Pocket Extras for Toaplan Version 2 Hardware Combination Core", async _ =>
            {
                await DownloadToaplan2cPocketExtras(path, coreUpdater);
                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        var menu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Update All", async _ =>
            {
                Console.WriteLine("Starting update process...");
                await coreUpdater.RunUpdates();
                Pause();
            })
            .Add("Update Firmware", async _ =>
            {
                await coreUpdater.UpdateFirmware();
                Pause();
            })
            .Add("Select Cores", () =>
            {
                AskAboutNewCores(true);
                RunCoreSelector(GlobalHelper.Cores);
                // Is reloading the settings file necessary?
                GlobalHelper.ReloadSettings();
            })
            .Add("Download Assets", async _ =>
            {
                Console.WriteLine("Checking for required files...");
                await coreUpdater.RunAssetDownloader();
                Pause();
            })
            .Add("Backup Saves", () =>
            {
                AssetsService.BackupSaves(path, GlobalHelper.SettingsManager.GetConfig().backup_saves_location);
                Pause();
            })
            .Add("Pocket Setup", pocketSetupMenu.Show)
            .Add("Pocket Maintenance", pocketMaintenanceMenu.Show)
            .Add("Pocket Extras", pocketExtrasMenu.Show)
            .Add("Settings", () =>
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
                    var title = MenuItemName(name, selected, isCoreSelection, core.requires_license);

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

                        thisMenu.CurrentItem.Name = MenuItemName(core.identifier, selected, isCoreSelection,
                            core.requires_license);
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
            { "download_gnw_roms", "Download Game & Watch ROMs during 'Update All'" },
            { "build_instance_jsons", "Build game JSON files for supported cores during 'Update All'" },
            { "delete_skipped_cores", "Delete untracked cores during 'Update All'" },
            { "fix_jt_names", "Automatically rename Jotego cores during 'Update All'" },
            { "crc_check", "Use CRC check when checking ROMs and BIOS files" },
            { "preserve_platforms_folder", "Preserve 'Platforms' folder during 'Update All'" },
            { "skip_alternative_assets", "Skip alternative roms when downloading assets" },
            { "backup_saves", "Compress and backup Saves and Memories directories during 'Update All'" },
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
            var value = (bool)property!.GetValue(GlobalHelper.SettingsManager.GetConfig())!;
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

    private static string MenuItemName(string title, bool value, bool isCoreSelection = false, bool requiresLicense = false)
    {
        string name = $"[{(value ? "x" : " ")}] {title}";

        if (isCoreSelection && requiresLicense)
        {
            name += " (Requires beta access)";
        }

        return name;
    }
}
