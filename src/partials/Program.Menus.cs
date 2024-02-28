using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.Extras;
using Pannella.Models.OpenFPGA_Cores_Inventory;
using Pannella.Models.Settings;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private static void DisplayMenuNew(CoreUpdaterService coreUpdaterService)
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
                PlatformImagePackSelector();
                Pause();
            })
            .Add("Download Pocket Library Images", _ =>
            {
                DownloadPockLibraryImages();
                Pause();
            })
            .Add("Download GameBoy Palettes", _ =>
            {
                DownloadGameBoyPalettes();
                Pause();
            })
            .Add("Generate Instance JSON Files (PC Engine CD)", () =>
            {
                RunInstanceGenerator(coreUpdaterService);
                Pause();
            })
            .Add("Generate Game & Watch ROMs", _ =>
            {
                BuildGameAndWatchRoms();
                Pause();
            })
            .Add("Enable All Display Modes", () =>
            {
                var cores = ServiceHelper.CoresService.Cores.Where(core =>
                    !ServiceHelper.SettingsService.GetCoreSettings(core.identifier).skip).ToList();

                foreach (var core in cores)
                {
                    if (core == null)
                    {
                        Console.WriteLine("Core name is required. Skipping");
                        return;
                    }

                    try
                    {
                        // not sure if this check is still needed
                        if (core.identifier == null)
                        {
                            Console.WriteLine("Core Name is required. Skipping.");
                            continue;
                        }

                        Console.WriteLine("Updating " + core.identifier);
                        ServiceHelper.CoresService.AddDisplayModes(core.identifier);
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
                    ServiceHelper.CoresService.InstalledCores.Where(c => c.identifier.StartsWith("Spiritualized.SuperGB")).ToList(),
                    "Which Super GameBoy cores would you like to change to the 8:7 aspect ratio?\n",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    var core = ServiceHelper.CoresService.InstalledCores.First(c => c.identifier == item.Key);

                    Console.WriteLine($"Updating '{core.identifier}'...");
                    ServiceHelper.CoresService.ChangeAspectRatio(core.identifier, 4, 3, 8, 7);
                    Console.WriteLine("Complete.");
                    Console.WriteLine();
                }

                Pause();
            })
            .Add("Restore 4:3 Aspect Ratio to Super GameBoy cores", () =>
            {
                var results = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores.Where(c => c.identifier.StartsWith("Spiritualized.SuperGB")).ToList(),
                    "Which Super GameBoy cores would you like to change to the 8:7 aspect ratio?\n",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    var core = ServiceHelper.CoresService.InstalledCores.First(c => c.identifier == item.Key);

                    Console.WriteLine($"Updating '{core.identifier}'...");
                    ServiceHelper.CoresService.ChangeAspectRatio(core.identifier, 8, 7, 4, 3);
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
                coreUpdaterService.RunUpdates(null, true);
                Pause();
            })
            .Add("Reinstall Select Cores", _ =>
            {
                var results = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores,
                    "Which cores would you like to reinstall?",
                    false);
                var identifiers = results.Where(x => x.Value)
                                               .Select(x => x.Key)
                                               .ToArray();

                coreUpdaterService.RunUpdates(identifiers, true);

                Pause();
            })
            .Add("Uninstall Select Cores", () =>
            {
                var results = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores,
                    "Which cores would you like to uninstall?",
                    false);

                bool nuke = AskYesNoQuestion("Would you like to remove the core specific assets for the selected cores?");

                foreach (var item in results.Where(x => x.Value))
                {
                    coreUpdaterService.DeleteCore(ServiceHelper.CoresService.GetCore(item.Key), true, nuke);
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

        foreach (var pocketExtra in ServiceHelper.CoresService.PocketExtrasList)
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

                if (ServiceHelper.SettingsService.GetConfig().show_menu_descriptions &&
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
                    ServiceHelper.CoresService.GetPocketExtra(pocketExtra, ServiceHelper.UpdateDirectory, true);
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
                coreUpdaterService.RunUpdates();
                ServiceHelper.CoresService.RefreshInstalledCores();
                Pause();
            })
            .Add("Update Firmware", _ =>
            {
                ServiceHelper.FirmwareService.UpdateFirmware(ServiceHelper.UpdateDirectory);
                Pause();
            })
            .Add("Select Cores            >", () => // \u00BB
            {
                AskAboutNewCores(true);
                RunCoreSelector(ServiceHelper.CoresService.Cores);
                // Is reloading the settings file necessary?
                ServiceHelper.ReloadSettings();
            })
            .Add("Download Assets", _ =>
            {
                Console.WriteLine("Checking for required files...");
                var cores = ServiceHelper.CoresService.Cores.Where(core =>
                    !ServiceHelper.SettingsService.GetCoreSettings(core.identifier).skip).ToList();

                ServiceHelper.CoresService.DownloadCoreAssets(cores);
                Pause();
            })
            .Add("Backup Saves & Memories", () =>
            {
                AssetsService.BackupSaves(ServiceHelper.UpdateDirectory,
                    ServiceHelper.SettingsService.GetConfig().backup_saves_location);
                AssetsService.BackupMemories(ServiceHelper.UpdateDirectory,
                    ServiceHelper.SettingsService.GetConfig().backup_saves_location);
                Pause();
            })
            .Add("Pocket Setup            >", pocketSetupMenu.Show)
            .Add("Pocket Maintenance      >", pocketMaintenanceMenu.Show)
            .Add("Pocket Extras           >", pocketExtrasMenu.Show)
            .Add("Settings                >", () =>
            {
                SettingsMenu();
                // Is reloading the settings file necessary?
                ServiceHelper.ReloadSettings();
            })
            .Add("Exit", ConsoleMenu.Close);

        menu.Show();
    }

    private static void AskAboutNewCores(bool force = false)
    {
        while (ServiceHelper.SettingsService.GetConfig().download_new_cores == null || force)
        {
            force = false;

            Console.WriteLine("Would you like to, by default, install new cores? [Y]es, [N]o, [A]sk for each:");

            ConsoleKey response = Console.ReadKey(true).Key;

            ServiceHelper.SettingsService.GetConfig().download_new_cores = response switch
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
                    var coreSettings = ServiceHelper.SettingsService.GetCoreSettings(core.identifier);
                    var selected = isCoreSelection && !coreSettings.skip;
                    var name = core.identifier;
                    var title = MenuItemName(name, selected, core.requires_license);

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

                        thisMenu.CurrentItem.Name = MenuItemName(core.identifier, selected, core.requires_license);
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
        if (ServiceHelper.SettingsService.GetConfig().download_new_cores?.ToLowerInvariant() == "yes")
        {
            foreach (Core core in cores)
            {
                ServiceHelper.SettingsService.EnableCore(core.identifier);
            }
        }
        else
        {
            var results = ShowCoresMenu(cores, message, true);

            foreach (var item in results)
            {
                if (item.Value)
                    ServiceHelper.SettingsService.EnableCore(item.Key);
                else
                    ServiceHelper.SettingsService.DisableCore(item.Key);
            }
        }

        ServiceHelper.SettingsService.GetConfig().core_selector = false;
        ServiceHelper.SettingsService.Save();
    }

    private static void PlatformImagePackSelector()
    {
        Console.Clear();

        if (ServiceHelper.PlatformImagePacksService.List.Count > 0)
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

            foreach (var pack in ServiceHelper.PlatformImagePacksService.List)
            {
                menu.Add($"{pack.owner}: {pack.repository} {pack.variant ?? string.Empty}", thisMenu =>
                {
                    choice = thisMenu.CurrentItem.Index;
                    thisMenu.CloseMenu();
                });
            }

            menu.Add("Go Back", thisMenu =>
            {
                choice = ServiceHelper.PlatformImagePacksService.List.Count;
                thisMenu.CloseMenu();
            });

            menu.Show();

            if (choice < ServiceHelper.PlatformImagePacksService.List.Count && choice >= 0)
            {
                ServiceHelper.PlatformImagePacksService.Install(
                    ServiceHelper.PlatformImagePacksService.List[choice].owner,
                    ServiceHelper.PlatformImagePacksService.List[choice].repository,
                    ServiceHelper.PlatformImagePacksService.List[choice].variant);
            }
            else if (choice == ServiceHelper.PlatformImagePacksService.List.Count)
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

    private static string MenuItemName(string title, bool value, bool requiresLicense = false)
    {
        string name = $"[{(value ? "x" : " ")}] {title}";

        if (requiresLicense)
        {
            name += " (Requires beta access)";
        }

        return name;
    }
}
