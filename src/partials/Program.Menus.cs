using System.ComponentModel;
using System.Reflection;
using ConsoleTools;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Settings;

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
        [Description("Download Pocket Extras for ericlewis.DonkeyKong")]
        DownloadPocketExtrasDonkeyKong,
        [Description("Settings")]
        Settings,
        [Description("Exit")]
        Exit
    }

    /*
     * Menu Structure
     * 0. Update All
     * 1. Update Firmware
     * 2. Select Cores
     *    -- Should this kick into installing the new cores that are selected right away?
     *    -- Should this be built into the menu system or dynamically generated like it is?
     * 3. Download Assets
     * 4. Backup Saves
     * 5. Pocket Setup
     *    a. Download Platform Image Packs
     *    b. Download Pocket Library Images -- NEW FEATURE
     *       -- only supported platforms GB, GBC, GG, maybe others when you can verify with someone who has the new adapters
     *       i.   Spiritualized1997 -- Dropbox link; should be moved to github or the archive maybe?
     *       ii.  CodeWario / libretro-thumbnails - Box Arts
     *       iii. CodeWario / libretro-thumbnails - Snaps
     *       iv.  CodeWario / libretro-thumbnails - Titles
     *       v.   Go Back
     *    c. Download GameBoy Palettes -- NEW FEATURE (Discord & github)
     *    d. Generate Instance JSON Files (PC Engine CD)
     *    e. Generate Game & Watch ROMs
     *    f. Enable All Display Modes
     *    g. Go Back
     * 6. Pocket Maintenance -- NEW FEATURE
     *    a. Reinstall Cores
     *    b. Uninstall Cores
     *       - Prompt: Do you want to delete the ROMs for the core?
     *       - Prompt: Do you want to delete the Saves for the core?
     *    c. Uninstall Pocket Extras
     *       -- provide a list of the installed extras like the uninstall feature
     *       -- uninstall the core with prompts
     *       -- reinstall the base core & download the assets if necessary
     *    d. Go Back
     * 7. Pocket Extras -- NEW FEATURE
     *    a. Download extras for Eric Lewis's Donkey Kong
     *    b. Download extras for Eric Lewis's Radar Scope -- this includes the DK roms too, what to do?
     *    c. Download extras for Jotego's Bubble Bobble (jtbubl) (beta)
     *    d. Download extras for Jotego's Capcom CPS 1 (jtcps1)
     *    e. Download extras for Jotego's Capcom CPS 1.5 (jtcps15)
     *    f. Download extras for Jotego's Capcom CPS 2 (jtcps2)
     *    g. Download extras for Jotego's Pang / Super Pang (jtpang)
     *    h. Toaplan 2 Single Platform Arcade Multi -- Not sure how to handle this one yet.
     *    i. Download all extras
     *    k. Go Back
     * 8. Settings
     * 9. Exit
     *
     * -- Where to put the Super GameBoy enhancements? (SGB2, 8x7) Maybe Pocket Extras? Talk to dyreschlock about adding them to his repo.
     * -- SGB Assets don't auto download because of being marked as required false.
     */

    private static MainMenuItems DisplayMenuNew()
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
            DescriptionAttribute[] attributes = (DescriptionAttribute[])fi!.GetCustomAttributes(typeof(DescriptionAttribute), false);
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
