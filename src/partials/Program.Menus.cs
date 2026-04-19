using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models.Extras;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;
using Pannella.Models.PocketLibraryImages;
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

        string sponsorLinks = GetRandomSponsorLinks();

        var menuConfig = new MenuConfig
        {
            Selector = "=>",
            EnableWriteTitle = false,
            WriteHeaderAction = () =>
            {
                WriteRainbow(welcome);
                Console.ResetColor();
                Console.WriteLine(sponsorLinks);
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

        var pocketLibraryImagesMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Spiritualized1997 (GB, GBC, GBA, GG)", _ =>
            {
                ServiceHelper.CoresService.DownloadPockLibraryImages();
                Pause();
            });

        foreach (PocketLibraryImageMenu libraryImageMenu in ServiceHelper.CoresService.PocketLibraryImagesList)
        {
            var subMenu = new ConsoleMenu().Configure(menuConfig);

            foreach (PocketLibraryImage image in libraryImageMenu.entries)
            {
                PocketLibraryImage img = image;
                string label = string.IsNullOrWhiteSpace(img.menu_label) ? img.id : img.menu_label.Trim();
                subMenu.Add(label, _ =>
                {
                    ServiceHelper.CoresService.DownloadPocketLibraryImages(img);
                    Pause();
                });
            }

            subMenu.Add("Go Back", ConsoleMenu.Close);

            string parentLabel = libraryImageMenu.menu_title.TrimEnd();
            if (!parentLabel.EndsWith('>'))
                parentLabel = string.Concat(parentLabel, " >");

            pocketLibraryImagesMenu.Add(parentLabel, subMenu.Show);
        }

        pocketLibraryImagesMenu.Add("Go Back", ConsoleMenu.Close);

        var downloadFilesMenu = new ConsoleMenu()
            .Configure(menuConfig)
            .Add("Download Platform Image Packs >", _ =>
            {
                PlatformImagePackSelector();
                Pause();
            })
            .Add("Download Pocket Library Images >", pocketLibraryImagesMenu.Show)
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
                        .Where(c => c.id.StartsWith("Spiritualized.SuperGB")).ToList(),
                    "Which Super GameBoy cores would you like to change to the 8:7 aspect ratio?\n",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    var core = ServiceHelper.CoresService.InstalledCores.First(c => c.id == item.Key);

                    Console.WriteLine($"Updating '{core.id}'...");
                    ServiceHelper.CoresService.ChangeAspectRatio(core.id, 4, 3, 8, 7);
                    Console.WriteLine("Complete.");
                    Console.WriteLine();
                }

                Pause();
            })
            .Add("Restore 4:3 Aspect Ratio to Super GameBoy cores", () =>
            {
                var results = ShowCoresMenu(
                    ServiceHelper.CoresService.InstalledCores
                        .Where(c => c.id.StartsWith("Spiritualized.SuperGB")).ToList(),
                    "Which Super GameBoy cores would you like to change to the 8:7 aspect ratio?\n",
                    false);

                foreach (var item in results.Where(x => x.Value))
                {
                    var core = ServiceHelper.CoresService.InstalledCores.First(c => c.id == item.Key);

                    Console.WriteLine($"Updating '{core.id}'...");
                    ServiceHelper.CoresService.ChangeAspectRatio(core.id, 8, 7, 4, 3);
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
                var config = ServiceHelper.SettingsService.Config;

                Console.WriteLine($"Current email address: {config.patreon_email_address}");

                var result = AskYesNoQuestion("Would you like to change your address?");

                if (!result)
                    return;

                string input = PromptForInput();
                string newEmail = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

                string previousComparable = string.IsNullOrWhiteSpace(config.patreon_email_address)
                    ? string.Empty
                    : config.patreon_email_address.Trim();
                string newComparable = newEmail ?? string.Empty;

                if (string.Equals(previousComparable, newComparable, StringComparison.InvariantCultureIgnoreCase))
                {
                    Pause();
                    return;
                }

                if (!config.coin_op_beta && newEmail != null)
                {
                    if (AskYesNoQuestion("Would you like to enable Coin-Op Collection beta access?"))
                    {
                        config.coin_op_beta = true;
                    }
                }

                config.patreon_email_address = newEmail;
                ServiceHelper.SettingsService.Save();

                Pause();
            })
            .Add("Set Patreon Session Cookie (for JT Beta auto-fetch)", () =>
            {
                var config = ServiceHelper.SettingsService.Config;

                Console.WriteLine($"Session cookie: {(string.IsNullOrWhiteSpace(config.patreon_session_cookie) ? "not set" : "set")}");
                Console.WriteLine("Note: session cookies expire periodically. If auto-fetch starts failing, re-paste a fresh one.");
                Console.WriteLine();
                Console.WriteLine("To get the cookie:");
                Console.WriteLine("  1. Open patreon.com in your browser and log in.");
                Console.WriteLine("  2. Open DevTools (F12 or Cmd+Opt+I).");
                Console.WriteLine("  3. Go to the Application tab (Chrome/Edge/Brave) or Storage tab (Firefox).");
                Console.WriteLine("  4. Expand Cookies > https://www.patreon.com.");
                Console.WriteLine("  5. Copy the Value of the 'session_id' row");

                var result = AskYesNoQuestion("Would you like to set/change the session cookie?");

                if (!result)
                    return;

                string input = PromptForInput();
                config.patreon_session_cookie = string.IsNullOrWhiteSpace(input) ? null : input.Trim();

                if (!config.jt_beta_patreon_fetch && config.patreon_session_cookie != null)
                {
                    if (AskYesNoQuestion("Would you like to enable JT Beta auto-fetch via Patreon?"))
                    {
                        config.jt_beta_patreon_fetch = true;
                    }
                }

                ServiceHelper.SettingsService.Save();

                Pause();
            })
            .Add("Test Patreon Session Cookie", () =>
            {
                string cookie = ServiceHelper.SettingsService.Config.patreon_session_cookie;

                if (string.IsNullOrWhiteSpace(cookie))
                {
                    Console.WriteLine("No Patreon session cookie is set. Use 'Set Patreon Session Cookie' first.");
                    Pause();
                    return;
                }

                Console.WriteLine("Testing Patreon session cookie (this hits /api/current_user and the Jotego campaign page)...");
                Console.WriteLine();

                var diag = PatreonService.TestSessionCookie(cookie, "jotego");

                foreach (var msg in diag.Messages)
                    Console.WriteLine("  - " + msg);

                Console.WriteLine();

                if (!diag.CookieValid)
                {
                    Console.WriteLine("RESULT: Cookie is NOT valid. Re-grab a fresh session_id from your browser.");
                }
                else if (diag.IsPatron)
                {
                    string status = string.IsNullOrEmpty(diag.PatronStatus) ? "unknown" : diag.PatronStatus;
                    string tier = string.IsNullOrEmpty(diag.TierName) ? "(no tier returned)" : diag.TierName;

                    Console.WriteLine($"RESULT: You ARE a Jotego patron. Status: {status}. Tier(s): {tier}.");
                    Console.WriteLine("Whether your tier includes beta access is decided per-post; auto-fetch will tell you if it can't see a beta post.");
                }
                else
                {
                    Console.WriteLine("RESULT: Cookie works, but this account is NOT currently a Jotego patron.");
                    Console.WriteLine("Auto-fetch will still attempt to find jtbeta.zip but will fail on the tier-gate check.");
                }

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
                ClearArchiveCache(promptForConfirmation: true);
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
                    !ServiceHelper.SettingsService.GetCoreSettings(core.id).skip).ToList();

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
        {
            ConsoleColor.Red,
            ConsoleColor.Yellow,
            ConsoleColor.Green,
            ConsoleColor.Cyan,
            ConsoleColor.Blue,
            ConsoleColor.Magenta,
        };
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

    private static void PinCoreVersionForCore(Core core)
    {
        var settings = ServiceHelper.SettingsService;
        var currentPin = settings.GetCoreSettings(core.id).pinned_version;

        if (core.releases == null || core.releases.Count == 0)
        {
            if (currentPin != null)
            {
                Console.WriteLine($"Currently pinned to: {currentPin}");
            }
            else
            {
                Console.WriteLine("Not currently pinned.");
            }

            Console.WriteLine("Enter version to pin to, or leave blank to remove pin:");

            string input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input))
            {
                settings.UnpinCoreVersion(core.id);
                Console.WriteLine("Version pin removed.");
            }
            else
            {
                settings.PinCoreVersion(core.id, input);
                Console.WriteLine($"Pinned to version: {input}");
            }

            settings.Save();
            return;
        }

        List<Release> releases = core.releases
            .Where(r => r.core?.metadata != null)
            .OrderByDescending(r => r.core.metadata.date_release ?? "")
            .ToList();

        bool[] step1DoneRef = { false };

        while (!step1DoneRef[0])
        {
            var step1Menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () => Console.WriteLine($"Pin/unpin version for {core.id}:");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                    config.WriteItemAction = item => Console.Write("{0}", item.Name);
                });

            step1Menu.Add("Unpin", () =>
            {
                settings.UnpinCoreVersion(core.id);
                settings.Save();
                Console.WriteLine("Version pin removed.");
                step1DoneRef[0] = true;
                step1Menu.CloseMenu();
            });

            step1Menu.Add("Select from releases list", () =>
            {
                step1Menu.CloseMenu();
                ShowReleaseListMenu(core, releases, settings, step1DoneRef);
            });

            step1Menu.Add("Enter version manually", () =>
            {
                Console.WriteLine("Enter version to pin to:");
                string input = Console.ReadLine()?.Trim();

                if (!string.IsNullOrEmpty(input))
                {
                    settings.PinCoreVersion(core.id, input);
                    settings.Save();
                    Console.WriteLine($"Pinned to version: {input}");
                }

                step1DoneRef[0] = true;
                step1Menu.CloseMenu();
            });

            step1Menu.Add("Go Back", () =>
            {
                step1DoneRef[0] = true;
                step1Menu.CloseMenu();
            });

            step1Menu.Show();
        }
    }

    private static void ShowReleaseListMenu(Core core, List<Release> releases, SettingsService settings, bool[] step1DoneRef)
    {
        const int pageSize = 12;
        int offset = 0;
        bool more = true;

        while (more)
        {
            var menu = new ConsoleMenu()
                .Configure(config =>
                {
                    config.Selector = "=>";
                    config.EnableWriteTitle = false;
                    config.WriteHeaderAction = () => Console.WriteLine($"Select a release for {core.id}:");
                    config.SelectedItemBackgroundColor = Console.ForegroundColor;
                    config.SelectedItemForegroundColor = Console.BackgroundColor;
                    config.WriteItemAction = item => Console.Write("{0}", item.Name);
                });
            int index = -1;

            if (offset + pageSize < releases.Count)
            {
                menu.Add("Next Page", thisMenu =>
                {
                    offset += pageSize;
                    thisMenu.CloseMenu();
                });
            }

            foreach (Release r in releases)
            {
                index++;

                if (index < offset || index > offset + pageSize)
                    continue;

                string ver = r.core?.metadata?.version ?? "?";
                string date = r.core?.metadata?.date_release ?? "";
                string label = string.IsNullOrEmpty(date) ? ver : $"{ver} ({date})";
                var capturedRelease = r;

                menu.Add(label, thisMenu =>
                {
                    settings.PinCoreVersion(core.id, capturedRelease.core.metadata.version);
                    settings.Save();
                    Console.WriteLine($"Pinned to version: {capturedRelease.core.metadata.version}");
                    step1DoneRef[0] = true;
                    more = false;
                    thisMenu.CloseMenu();
                });
            }

            if (offset + pageSize < releases.Count)
            {
                menu.Add("Next Page", thisMenu =>
                {
                    offset += pageSize;
                    thisMenu.CloseMenu();
                });
            }

            if (offset > 0)
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
                var pinned = ServiceHelper.SettingsService.GetCoreSettings(captured.id).pinned_version;
                string label = pinned != null
                    ? $"{captured.id} [pinned: {pinned}]"
                    : captured.id;

                menu.Add(label, thisMenu =>
                {
                    thisMenu.CloseMenu();
                    PinCoreVersionForCore(captured);
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
