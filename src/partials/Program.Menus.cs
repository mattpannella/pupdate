using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.Extras;
using Pannella.Services;

namespace Pannella;

internal static partial class Program
{
    private static void DisplayMenu(CoreUpdaterService coreUpdaterService)
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
            EnableWriteTitle = false,
            WriteHeaderAction = () =>
            {
                WriteRainbow(welcome);
                Console.ResetColor();
                Console.WriteLine(GetRandomSponsorLinks());
                Console.WriteLine(rateLimitMessage);
                Console.WriteLine();
                Console.WriteLine("Choose your destiny:");
            },
            SelectedItemBackgroundColor = Console.ForegroundColor,
            SelectedItemForegroundColor = Console.BackgroundColor
        };

        #region Pocket Setup - Display Modes

        var displayModesMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Enable Recommended Display Modes", () =>
            {
                EnableDisplayModes(isCurated: true);
                Pause();
            })
            .Add("Enable Selected Display Modes", () =>
            {
                DisplayModeSelector();
                Pause();
            })
            .Add("Enable Selected Display Modes for Selected Cores", () =>
            {
                DisplayModeSelector(true);
                Pause();
            })
            .Add("Reset All Customized Display Modes", () =>
            {
                ResetDisplayModes();
                Pause();
            })
            .Add("Reset Selected Customized Display Modes", () =>
            {
                var coreResults = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores,
                    "Which cores would you like to reset the display modes for?",
                    false);
                var coreIdentifiers = coreResults.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToList();

                ResetDisplayModes(coreIdentifiers);
                Pause();
            })
            .Add("Change Display Modes Option Setting", () =>
            {
                 AskAboutDisplayModesSetting(true);
                 Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        #endregion

        #region Pocket Setup - Download Files

        var downloadFilesMenu = new ConsoleMenu()
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
            .Add("Go Back", ConsoleMenu.Close);

        #endregion

        #region Pocket Setup - Generate Files

        var generateFilesMenu = new ConsoleMenu()
            .Configure(menuConfig)
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
            .Add("Go Back", ConsoleMenu.Close);

        #endregion

        #region Pocket Setup - Super GameBoy Aspect Ratio

        var sgbAspectRatioMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Apply 8:7 Aspect Ratio to Super GameBoy cores", () =>
            {
                var results = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores
                        .Where(c => c.identifier.StartsWith("Spiritualized.SuperGB")).ToList(),
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
                    ServiceHelper.CoresService.InstalledCores
                        .Where(c => c.identifier.StartsWith("Spiritualized.SuperGB")).ToList(),
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

        #endregion

        #region Analogizer Setup

        var analogizerMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Jotego Analogizer Config", _=>
            {
                JotegoAnalogizerSettingsService settings = new JotegoAnalogizerSettingsService();
                settings.RunAnalogizerSettings();

                Console.WriteLine("Jotego Analogizer configuration updated.");
                Pause();
            })
            .Add("Standard Analogizer Config", _=>
            {
                AnalogizerSettingsService.ShowWizard();

                Console.WriteLine("Analogizer configuration updated.");
                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        #endregion

        #region Pocket Setup

        var pocketSetupMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Manage Display Modes         >", displayModesMenu.Show)
            .Add("Download Images and Palettes >", downloadFilesMenu.Show)
            .Add("Generate ROMs & JSON Files   >", generateFilesMenu.Show)
            .Add("Super GameBoy Aspect Ratio   >", sgbAspectRatioMenu.Show)
            .Add("Analogizer Config            >", analogizerMenu.Show)
            .Add("Set Patreon Email Address", () =>
            {
                Console.WriteLine($"Current email address: {ServiceHelper.SettingsService.Config.patreon_email_address}");
                var result = AskYesNoQuestion("Would you like to change your address?");

                if (!result)
                    return;

                string input = PromptForInput();
                ServiceHelper.SettingsService.Config.patreon_email_address = input;
                ServiceHelper.SettingsService.Save();

                Pause();
            })
            .Add("Print openFPGA Category Structure", () =>
            {
                PrintOpenFpgaCategories();
                Pause();
            })
            .Add("Go Back", ConsoleMenu.Close);

        #endregion

        #region Pocket Maintenance

        var pocketMaintenanceMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Update Selected", _ =>
            {
                var selectedCores = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores,
                    "Which cores would you like to update?",
                    false);
                var list = selectedCores.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();

                if (list.Length > 0)
                {
                    coreUpdaterService.RunUpdates(list);
                }
                Pause();
            })
            .Add("Install Selected", _ =>
            {
                var selectedCores = RunCoreSelector(
                    ServiceHelper.CoresService.CoresNotInstalled,
                    "Which cores would you like pupdate to manage and install?");
                var list = selectedCores.Where(kvp => kvp.Value).Select(kvp => kvp.Key).ToArray();

                if (list.Length > 0)
                {
                    coreUpdaterService.RunUpdates(list);
                }

                Pause();
            })
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
                var identifiers = results
                    .Where(x => x.Value)
                    .Select(x => x.Key)
                    .ToArray();

                if (identifiers.Length > 0)
                {
                    coreUpdaterService.RunUpdates(identifiers, true);

                    Pause();
                }
            })
            .Add("Uninstall Select Cores", () =>
            {
                var results = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores,
                    "Which cores would you like to uninstall?",
                    false);
                var selectResults = results.Where(x => x.Value).ToList();

                if (selectResults.Count > 0)
                {
                    bool nuke = AskYesNoQuestion("Would you like to remove the core specific assets for the selected cores?");

                    foreach (var item in selectResults)
                    {
                        coreUpdaterService.DeleteCore(ServiceHelper.CoresService.GetCore(item.Key), true, nuke);
                    }

                    Pause();
                }
            })
            .Add("Manage ROM Set Archives", () =>
            {
                ShowArchiveManagementMenu();
            })
            .Add("Prune Save States", _ =>
            {
                AssetsService.PruneSaveStates(ServiceHelper.UpdateDirectory);
                Pause();
            })
            .Add("Clear Archive Cache", () =>
            {
                if (!ServiceHelper.SettingsService.Config.cache_archive_files)
                {
                    Console.WriteLine("Archive caching is not enabled.");
                    Pause();
                    return;
                }

                string cacheDir = ServiceHelper.CacheDirectory;

                if (!Directory.Exists(cacheDir))
                {
                    Console.WriteLine("Cache directory is already empty.");
                    Pause();
                    return;
                }

                if (AskYesNoQuestion("Are you sure you want to clear the archive cache?"))
                {
                    Directory.Delete(cacheDir, recursive: true);
                    Console.WriteLine("Archive cache cleared.");
                }

                Pause();
            })
            .Add("Pin/Unpin Core Version", () =>
            {
                PinCoreVersionMenu();
            })
            .Add("Go Back", ConsoleMenu.Close);

        #endregion

        #region Pocket Extras

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
            var name = string.IsNullOrWhiteSpace(pocketExtra.name) // name is not required for additional assets
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

                if (ServiceHelper.SettingsService.Config.show_menu_descriptions &&
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
                    ServiceHelper.CoresService.RefreshLocalCores();
                    ServiceHelper.CoresService.RefreshInstalledCores();
                    Pause();
                }
            });
        }

        additionalAssetsMenu.Add("Go Back", ConsoleMenu.Close);
        combinationPlatformsMenu.Add("Go Back", ConsoleMenu.Close);
        variantCoresMenu.Add("Go Back", ConsoleMenu.Close);

        #endregion

        #region Main Menu

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
                    ServiceHelper.SettingsService.Config.backup_saves_location);
                AssetsService.BackupMemories(ServiceHelper.UpdateDirectory,
                    ServiceHelper.SettingsService.Config.backup_saves_location);
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
                coreUpdaterService.ReloadSettings();
            })
            .Add("Exit", ConsoleMenu.Close);

        #endregion

        menu.Show();
    }

    private static string PromptForInput()
    {
        Console.Write("Enter value: ");

        string value = Console.ReadLine();

        return value;
    }

    private static void WriteRainbow(string text)
    {
        ConsoleColor[] colors =
        [
            ConsoleColor.Red,
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Blue,
            ConsoleColor.Magenta,
        ];
        int colorIndex = 0;

        foreach (char c in text)
        {
            if (!char.IsWhiteSpace(c))
            {
                Console.ForegroundColor = colors[colorIndex % colors.Length];
                colorIndex++;
            }

            Console.Write(c);
        }

        Console.WriteLine();
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

    private static void PinCoreVersionMenu()
    {
        const int pageSize = 12;
        var offset = 0;
        bool more = true;
        var cores = ServiceHelper.CoresService.InstalledCores
            .Where(c => c.repository != null)
            .ToList();

        if (!cores.Any())
        {
            Console.WriteLine("No pinnable cores installed.");
            return;
        }

        while (more)
        {
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () => Console.WriteLine("Select a core to pin/unpin:");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                    config.WriteItemAction = item => Console.Write("{0}", item.Name);
                });

            if (offset + pageSize <= cores.Count)
            {
                menu.Add("Next Page", thisMenu =>
                {
                    offset += pageSize;
                    thisMenu.CloseMenu();
                });
            }

            var current = -1;

            foreach (var core in cores)
            {
                current++;

                if (current < offset || current > offset + pageSize)
                    continue;

                var captured = core;
                var pinned = ServiceHelper.SettingsService.GetCoreSettings(captured.identifier).pinned_version;
                string label = pinned != null
                    ? $"{captured.identifier} [pinned: {pinned}]"
                    : captured.identifier;

                menu.Add(label, thisMenu =>
                {
                    thisMenu.CloseMenu();

                    var currentPin = ServiceHelper.SettingsService.GetCoreSettings(captured.identifier).pinned_version;

                    if (currentPin != null)
                        Console.WriteLine($"Currently pinned to: {currentPin}");
                    else
                        Console.WriteLine("Not currently pinned.");

                    Console.WriteLine("Enter version to pin to, or leave blank to remove pin:");
                    string input = Console.ReadLine()?.Trim();

                    if (string.IsNullOrEmpty(input))
                    {
                        ServiceHelper.SettingsService.UnpinCoreVersion(captured.identifier);
                        Console.WriteLine("Version pin removed.");
                    }
                    else
                    {
                        ServiceHelper.SettingsService.PinCoreVersion(captured.identifier, input);
                        Console.WriteLine($"Pinned to version: {input}");
                    }

                    ServiceHelper.SettingsService.Save();
                    Pause();
                });
            }

            if (offset + pageSize <= cores.Count)
            {
                menu.Add("Next Page", thisMenu =>
                {
                    offset += pageSize;
                    thisMenu.CloseMenu();
                });
            }

            if (offset != 0)
            {
                menu.Add("Prev Page", thisMenu =>
                {
                    offset -= pageSize;
                    thisMenu.CloseMenu();
                });
            }

            menu.Add("Go Back", thisMenu =>
            {
                more = false;
                thisMenu.CloseMenu();
            });

            menu.Show();
        }
    }

    private static void ShowArchiveManagementMenu()
    {
        bool syncFirst = AskYesNoQuestion("Would you like to sync your archives with the latest definitions?");
        
        if (syncFirst)
        {
            try
            {
                Console.WriteLine("Syncing archives...");
                ServiceHelper.SettingsService.SyncRomsets();
                Console.WriteLine("Archives synced successfully.");
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error syncing archives: {ex.Message}");
                Pause();
                return;
            }
        }

        var archives = ServiceHelper.SettingsService.Config.archives
            .Where(a => a.type == Models.Settings.ArchiveType.core_specific_archive)
            .ToList();

        if (archives.Count == 0)
        {
            Console.WriteLine("No archives found.");
            Pause();
            return;
        }

        bool keepOpen = true;

        while (keepOpen)
        {
            var menuConfig = new MenuConfig
            {
                Selector = "=>",
                EnableWriteTitle = false,
                WriteHeaderAction = () => Console.WriteLine("Select an archive to manage:"),
                SelectedItemBackgroundColor = Console.ForegroundColor,
                SelectedItemForegroundColor = Console.BackgroundColor
            };

            var archiveMenu = new ConsoleMenu().Configure(menuConfig);

            foreach (var archive in archives)
            {
                archiveMenu.Add(archive.name, () =>
                {
                    ShowArchiveOptionsMenu(archive);
                });
            }

            archiveMenu.Add("Go Back", thisMenu =>
            {
                keepOpen = false;
                thisMenu.CloseMenu();
            });

            archiveMenu.Show();
        }
    }

    private static void ShowArchiveOptionsMenu(Models.Settings.Archive archive)
    {
        var menuConfig = new MenuConfig
        {
            Selector = "=>",
            EnableWriteTitle = false,
            WriteHeaderAction = () => Console.WriteLine($"Managing: {archive.name}\nEnabled: {archive.enabled} | Complete: {archive.complete}"),
            SelectedItemBackgroundColor = Console.ForegroundColor,
            SelectedItemForegroundColor = Console.BackgroundColor
        };

        var optionsMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add($"Toggle Enable/Disable (Currently: {(archive.enabled ? "Enabled" : "Disabled")})", () =>
            {
                archive.enabled = !archive.enabled;
                ServiceHelper.SettingsService.Save();
                Console.WriteLine($"Archive is now {(archive.enabled ? "enabled" : "disabled")}.");
                Pause();
            })
            .Add("Mark as Incomplete", () =>
            {
                archive.complete = false;
                ServiceHelper.SettingsService.Save();
                Console.WriteLine("Archive marked as incomplete.");
                Pause();
            })
            .Add("Go Back", thisMenu => thisMenu.CloseMenu());

        optionsMenu.Show();
    }
}
