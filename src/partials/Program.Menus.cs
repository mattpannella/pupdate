using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Extras;
using Pannella.Models.OpenFPGA_Cores_Inventory;
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
        int remaining = GithubApiService.RemainingCalls;
        string rateLimitMessage = $"Remaining GitHub API calls: {remaining}";

        if (remaining <= 5)
        {
            rateLimitMessage = string.Concat(rateLimitMessage, Environment.NewLine,
                "Consider adding a Github Token to your settings file, to avoid hitting the rate limit.");
        }

        var menuConfig = new MenuConfig
        {
            Selector = "=>",
            Title = string.Concat(
                        welcome,
                        Environment.NewLine,
                        GetRandomSponsorLinks(),
                        Environment.NewLine,
                        rateLimitMessage,
                        Environment.NewLine),
            EnableWriteTitle = true,
            WriteHeaderAction = () => Console.WriteLine("Choose your destiny:"),
            SelectedItemBackgroundColor = Console.ForegroundColor,
            SelectedItemForegroundColor = Console.BackgroundColor,
        };

        var pocketSetupMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Download Platform Image Packs", _ =>
            {
                PlatformImagePackSelector(path);
                Pause();
            })
            .Add("Download Pocket Library Images", _ =>
            {
                DownloadPockLibraryImages(path);
                Pause();
            })
            .Add("Download GameBoy Palettes", _ =>
            {
                DownloadGameBoyPalettes(path);
                Pause();
            })
            .Add("Generate Instance JSON Files (PC Engine CD)", () =>
            {
                RunInstanceGenerator(coreUpdater);
                Pause();
            })
            .Add("Generate Game & Watch ROMs", _ =>
            {
                BuildGameAndWatchRoms(path);
                Pause();
            })
            .Add("Enable All Display Modes", () =>
            {
                var cores = GlobalHelper.Cores.Where(core =>
                    !GlobalHelper.SettingsManager.GetCoreSettings(core.identifier).skip).ToList();

                foreach (var core in cores)
                {
                    if (core == null)
                    {
                        Console.WriteLine("Core name is required. Skipping");
                        return;
                    }

                    core.download_assets = true;

                    try
                    {
                        // not sure if this check is still needed
                        if (core.identifier == null)
                        {
                            Console.WriteLine("Core Name is required. Skipping.");
                            continue;
                        }

                        Console.WriteLine("Updating " + core.identifier);
                        core.AddDisplayModes();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine("Uh oh something went wrong.");
#if DEBUG
                        Console.WriteLine(e.ToString());
#else
                        Console.WriteLine(e.Message);
#endif
                    }
                }

                Console.WriteLine("Finished.");
                Pause();
            })
            .Add("Apply 8:7 Aspect Ratio to Super GameBoy cores", () =>
            {
                var results = ShowCoresMenu(
                    GlobalHelper.InstalledCores.Where(c => c.identifier.StartsWith("Spiritualized.SuperGB")).ToList(),
                    "Which Super GameBoy cores would you like to change to the 8:7 aspect ratio?\n",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    var core = GlobalHelper.InstalledCores.First(c => c.identifier == item.Key);

                    Console.WriteLine($"Updating '{core.identifier}'...");
                    core.ChangeAspectRatio(4, 3, 8, 7);
                    Console.WriteLine("Complete.");
                    Console.WriteLine();
                }

                Pause();
            })
            .Add("Restore 4:3 Aspect Ratio to Super GameBoy cores", () =>
            {
                var results = ShowCoresMenu(
                    GlobalHelper.InstalledCores.Where(c => c.identifier.StartsWith("Spiritualized.SuperGB")).ToList(),
                    "Which Super GameBoy cores would you like to change to the 8:7 aspect ratio?\n",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    var core = GlobalHelper.InstalledCores.First(c => c.identifier == item.Key);

                    Console.WriteLine($"Updating '{core.identifier}'...");
                    core.ChangeAspectRatio(8, 7, 4, 3);
                    Console.WriteLine("Complete.");
                    Console.WriteLine();
                }

                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        var pocketMaintenanceMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Reinstall All Cores", _ =>
            {
                coreUpdater.RunUpdates(null, true);
                Pause();
            })
            .Add("Reinstall Select Cores", _ =>
            {
                var results = ShowCoresMenu(
                    GlobalHelper.InstalledCores,
                    "Which cores would you like to reinstall?",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    coreUpdater.RunUpdates(item.Key, true);
                }

                Pause();
            })
            .Add("Uninstall Select Cores", () =>
            {
                var results = ShowCoresMenu(
                    GlobalHelper.InstalledCores,
                    "Which cores would you like to uninstall?",
                    false);

                bool nuke = AskYesNoQuestion("Would you like to remove the core specific assets for the selected cores?");

                foreach (var item in results.Where(x => x.Value))
                {
                    coreUpdater.DeleteCore(GlobalHelper.GetCore(item.Key), true, nuke);
                }

                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        var additionalAssetsMenu = new ConsoleMenu().Configure(menuConfig);
        var combinationPlatformsMenu = new ConsoleMenu().Configure(menuConfig);
        var variantCoresMenu = new ConsoleMenu().Configure(menuConfig);
        var pocketExtrasMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Additional Assets     >", additionalAssetsMenu.Show)
            .Add("Combination Platforms >", combinationPlatformsMenu.Show)
            .Add("Variant Cores         >", variantCoresMenu.Show)
            .Add("Go Back", ConsoleMenu.Close);

        foreach (var pocketExtra in GlobalHelper.PocketExtras)
        {
            var name = string.IsNullOrWhiteSpace(pocketExtra.name)
                ? $"Download extras for {pocketExtra.core_identifiers[0]}"
                : $"Download {pocketExtra.name}";

            var consoleMenu = pocketExtra.type switch
            {
                PocketExtraType.additional_assets => additionalAssetsMenu,
                PocketExtraType.combination_platform => combinationPlatformsMenu,
                PocketExtraType.variant_core => variantCoresMenu,
                _ => pocketExtrasMenu
            };

            consoleMenu.Add(name, _ =>
            {
                bool result = true;

                if (GlobalHelper.SettingsManager.GetConfig().show_menu_descriptions &&
                    !string.IsNullOrEmpty(pocketExtra.description))
                {
                    Console.WriteLine(Util.WordWrap(pocketExtra.description, 80));
                    Console.WriteLine($"More info: https://github.com/{pocketExtra.github_user}/{pocketExtra.github_repository}");

                    foreach (var additionalLink in pocketExtra.additional_links)
                    {
                        Console.WriteLine($"           {additionalLink}");
                    }

                    Console.WriteLine();

                    result = AskYesNoQuestion("Would you like to install this?");
                }

                if (result)
                {
                    GlobalHelper.PocketExtrasService.GetPocketExtra(pocketExtra, path, true, true);
                    Pause();
                }
            });
        }

        additionalAssetsMenu.Add("Go Back", ConsoleMenu.Close);
        combinationPlatformsMenu.Add("Go Back", ConsoleMenu.Close);
        variantCoresMenu.Add("Go Back", ConsoleMenu.Close);

        var menu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Update All", _ =>
            {
                Console.WriteLine("Starting update process...");
                coreUpdater.RunUpdates();
                Pause();
            })
            .Add("Update Firmware", _ =>
            {
                GlobalHelper.FirmwareService.UpdateFirmware(GlobalHelper.UpdateDirectory);
                Pause();
            })
            .Add("Select Cores            >", () => // \u00BB
            {
                AskAboutNewCores(true);
                RunCoreSelector(GlobalHelper.Cores);
                // Is reloading the settings file necessary?
                GlobalHelper.ReloadSettings();
            })
            .Add("Download Assets", _ =>
            {
                Console.WriteLine("Checking for required files...");
                coreUpdater.RunAssetDownloader();
                Pause();
            })
            .Add("Backup Saves & Memories", () =>
            {
                AssetsService.BackupSaves(path, GlobalHelper.SettingsManager.GetConfig().backup_saves_location);
                AssetsService.BackupMemories(path, GlobalHelper.SettingsManager.GetConfig().backup_saves_location);
                Pause();
            })
            .Add("Pocket Setup            >", pocketSetupMenu.Show)
            .Add("Pocket Maintenance      >", pocketMaintenanceMenu.Show)
            .Add("Pocket Extras           >", pocketExtrasMenu.Show)
            .Add("Settings                >", () =>
            {
                SettingsMenu();

                coreUpdater.UpdateSettings(
                    GlobalHelper.SettingsManager.GetConfig().fix_jt_names,
                    GlobalHelper.SettingsManager.GetConfig().download_assets,
                    null, // Should this be updated if the setting is changed?
                    GlobalHelper.SettingsManager.GetConfig().download_firmware,
                    GlobalHelper.SettingsManager.GetConfig().backup_saves,
                    GlobalHelper.SettingsManager.GetConfig().backup_saves_location,
                    GlobalHelper.SettingsManager.GetConfig().delete_skipped_cores);

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

    private static bool AskYesNoQuestion(string question)
    {
        Console.WriteLine($"{question} [Y]es, [N]o");

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

    private static void PlatformImagePackSelector(string path)
    {
        Console.Clear();

        if (GlobalHelper.PlatformImagePacks.Count > 0)
        {
            int choice = 0;
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () => Console.WriteLine("So, what'll it be?:");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                });

            foreach (var pack in GlobalHelper.PlatformImagePacks)
            {
                menu.Add($"{pack.owner}: {pack.repository} {pack.variant ?? string.Empty}", thisMenu =>
                {
                    choice = thisMenu.CurrentItem.Index;
                    thisMenu.CloseMenu();
                });
            }

            menu.Add("Go Back", thisMenu =>
            {
                choice = GlobalHelper.PlatformImagePacks.Count;
                thisMenu.CloseMenu();
            });

            menu.Show();

            if (choice < GlobalHelper.PlatformImagePacks.Count && choice >= 0)
            {
                GlobalHelper.PlatformImagePacksService.Install(path, GlobalHelper.PlatformImagePacks[choice].owner,
                    GlobalHelper.PlatformImagePacks[choice].repository, GlobalHelper.PlatformImagePacks[choice].variant,
                    GlobalHelper.SettingsManager.GetConfig().github_token);
            }
            else if (choice == GlobalHelper.PlatformImagePacks.Count)
            {
                // What causes this?
            }
            else
            {
                Console.WriteLine("You fucked up.");
            }
        }
        else
        {
            Console.WriteLine("None found. Have a nice day.");
        }
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
            { "show_menu_descriptions", "Show descriptions for advanced menu items" },
            { "use_custom_archive", "Use custom asset archive" },
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
