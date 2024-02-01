using System.Diagnostics;
using CommandLine;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Options;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private static bool CLI_MODE;

    private static async Task Main(string[] args)
    {
        try
        {
            string location = Process.GetCurrentProcess().MainModule.FileName;
            string path = Path.GetDirectoryName(location);
            bool preservePlatformsFolder = false;
            bool forceUpdate = false;
            bool forceInstanceGenerator = false;
            string downloadAssets = null;
            string coreName = null;
            string imagePackOwner = null;
            string imagePackRepo = null;
            string imagePackVariant = null;
            bool downloadFirmware = false;
            bool selfUpdate = false;
            bool nuke = false;
            bool cleanInstall = false;
            string backupSaves_Path = null;
            bool backupSaves_SaveConfig = false;

            string verb = "menu";
            Dictionary<string, object> data = new Dictionary<string, object>();

            #region Command Line Arguments

            Parser.Default.ParseArguments<MenuOptions, FundOptions, UpdateOptions,
                    AssetsOptions, FirmwareOptions, ImagesOptions, InstanceGeneratorOptions,
                    UpdateSelfOptions, UninstallOptions, BackupSavesOptions>(args)
                .WithParsed<UpdateSelfOptions>(_ => { selfUpdate = true; })
                .WithParsed<FundOptions>(o =>
                    {
                        verb = "fund";
                        data.Add("core", null);

                        if (!string.IsNullOrEmpty(o.Core))
                        {
                            data["core"] = o.Core;
                        }
                    })
                .WithParsed<UpdateOptions>(o =>
                    {
                        verb = "update";
                        CLI_MODE = true;
                        forceUpdate = true;

                        if (!string.IsNullOrEmpty(o.InstallPath))
                        {
                            path = o.InstallPath;
                        }

                        if (o.PreservePlatformsFolder)
                        {
                            preservePlatformsFolder = true;
                        }

                        if (o.CleanInstall)
                        {
                            cleanInstall = true;
                        }

                        if (!string.IsNullOrEmpty(o.CoreName))
                        {
                            coreName = o.CoreName;
                        }
                    })
                .WithParsed<UninstallOptions>(o =>
                    {
                        verb = "uninstall";
                        CLI_MODE = true;
                        coreName = o.CoreName;

                        if (!string.IsNullOrEmpty(o.InstallPath))
                        {
                            path = o.InstallPath;
                        }

                        if (o.DeleteAssets)
                        {
                            nuke = true;
                        }
                    })
                .WithParsed<AssetsOptions>(o =>
                    {
                        verb = "assets";
                        CLI_MODE = true;
                        downloadAssets = "all";

                        if (!string.IsNullOrEmpty(o.InstallPath))
                        {
                            path = o.InstallPath;
                        }

                        if (o.CoreName != null)
                        {
                            downloadAssets = o.CoreName;
                        }
                    })
                .WithParsed<FirmwareOptions>(o =>
                    {
                        verb = "firmware";
                        CLI_MODE = true;
                        downloadFirmware = true;

                        if (!string.IsNullOrEmpty(o.InstallPath))
                        {
                            path = o.InstallPath;
                        }
                    })
                .WithParsed<ImagesOptions>(o =>
                    {
                        verb = "images";
                        CLI_MODE = true;

                        if (!string.IsNullOrEmpty(o.InstallPath))
                        {
                            path = o.InstallPath;
                        }

                        if (o.ImagePackOwner != null)
                        {
                            imagePackOwner = o.ImagePackOwner;
                            imagePackRepo = o.ImagePackRepo;
                            imagePackVariant = o.ImagePackVariant;
                        }
                    })
                .WithParsed<InstanceGeneratorOptions>(o =>
                    {
                        verb = "instancegenerator";
                        CLI_MODE = true;
                        forceInstanceGenerator = true;

                        if (!string.IsNullOrEmpty(o.InstallPath))
                        {
                            path = o.InstallPath;
                        }
                    })
                .WithParsed<MenuOptions>(o =>
                    {
                        if (!string.IsNullOrEmpty(o.InstallPath))
                        {
                            path = o.InstallPath;
                        }

                        if (o.SkipUpdate)
                        {
                            CLI_MODE = true;
                        }
                    })
                .WithParsed<BackupSavesOptions>(o =>
                    {
                        verb = "backup-saves";
                        CLI_MODE = true;
                        path = o.InstallPath;
                        backupSaves_Path = o.BackupPath;
                        backupSaves_SaveConfig = o.Save;
                    })
                .WithNotParsed(o =>
                    {
                        if (o.IsHelp())
                        {
                            Environment.Exit(1);
                        }

                        if (o.IsVersion())
                        {
                            Environment.Exit(1);
                        }
                    });

            #endregion

            GlobalHelper.Initialize(path);

            if (!CLI_MODE)
            {
                Console.WriteLine("Pupdate v" + VERSION);
                Console.WriteLine("Checking for updates...");

                if (await CheckVersion(path) && !selfUpdate)
                {
                    ConsoleKey[] acceptedInputs = { ConsoleKey.I, ConsoleKey.C, ConsoleKey.Q };
                    ConsoleKey response;

                    do
                    {
                        if (SYSTEM_OS_PLATFORM is "win" or "linux" or "mac")
                        {
                            Console.Write("Would you like to [i]nstall the update, [c]ontinue with the current version, or [q]uit? [i/c/q]: ");
                        }
                        else
                        {
                            Console.Write("Update downloaded. Would you like to [c]ontinue with the current version, or [q]uit? [c/q]: ");
                        }

                        response = Console.ReadKey(false).Key;
                        Console.WriteLine();
                    }
                    while (!acceptedInputs.Contains(response));

                    switch (response)
                    {
                        case ConsoleKey.I:
                            int result = UpdateSelfAndRun(path, args);
                            Environment.Exit(result);
                            break;

                        case ConsoleKey.C:
                            break;

                        case ConsoleKey.Q:
                            Console.WriteLine("Come again soon");
                            PauseExit();
                            break;
                    }
                }

                if (selfUpdate)
                {
                    Environment.Exit(0);
                }
            }

            PocketCoreUpdater coreUpdater = new PocketCoreUpdater();

            switch (verb)
            {
                case "fund":
                    Funding((string)data["core"]);
                    Environment.Exit(1);
                    break;
            }

            // how should the logic work here? what takes priority, the command line parameter or the config setting?
            // currently this well preserve the platforms folder if either is set to true
            if (preservePlatformsFolder || GlobalHelper.SettingsManager.GetConfig().preserve_platforms_folder)
            {
                coreUpdater.PreservePlatformsFolder(true);
            }

            coreUpdater.DeleteSkippedCores(GlobalHelper.SettingsManager.GetConfig().delete_skipped_cores);
            coreUpdater.DownloadFirmware(GlobalHelper.SettingsManager.GetConfig().download_firmware);
            coreUpdater.RenameJotegoCores(GlobalHelper.SettingsManager.GetConfig().fix_jt_names);
            coreUpdater.StatusUpdated += coreUpdater_StatusUpdated;
            coreUpdater.UpdateProcessComplete += coreUpdater_UpdateProcessComplete;
            coreUpdater.DownloadAssets(GlobalHelper.SettingsManager.GetConfig().download_assets);
            coreUpdater.BackupSaves(GlobalHelper.SettingsManager.GetConfig().backup_saves, GlobalHelper.SettingsManager.GetConfig().backup_saves_location);

            //await coreUpdater.Initialize();

            // If we have any missing cores, handle them.
            if (GlobalHelper.SettingsManager.GetMissingCores().Any())
            {
                Console.WriteLine("\nNew cores found since the last run.");
                AskAboutNewCores();

                string downloadNewCores = GlobalHelper.SettingsManager.GetConfig().download_new_cores?.ToLowerInvariant();

                switch (downloadNewCores)
                {
                    case "yes":
                        Console.WriteLine("The following cores have been enabled:");

                        foreach (Core core in GlobalHelper.SettingsManager.GetMissingCores())
                        {
                            Console.WriteLine($"- {core.identifier}");
                        }

                        GlobalHelper.SettingsManager.EnableMissingCores(GlobalHelper.SettingsManager.GetMissingCores());
                        GlobalHelper.SettingsManager.SaveSettings();
                        break;

                    case "no":
                        Console.WriteLine("The following cores have been disabled:");

                        foreach (Core core in GlobalHelper.SettingsManager.GetMissingCores())
                        {
                            Console.WriteLine($"- {core.identifier}");
                        }

                        GlobalHelper.SettingsManager.DisableMissingCores(GlobalHelper.SettingsManager.GetMissingCores());
                        GlobalHelper.SettingsManager.SaveSettings();
                        break;

                    default:
                        var newOnes = GlobalHelper.SettingsManager.GetMissingCores();

                        GlobalHelper.SettingsManager.EnableMissingCores(newOnes);

                        if (CLI_MODE)
                        {
                            GlobalHelper.SettingsManager.SaveSettings();
                        }
                        else
                        {
                            RunCoreSelector(newOnes, "New cores are available!");
                        }

                        break;
                }

                // Is reloading the settings file necessary?
                GlobalHelper.ReloadSettings();
            }

            if (forceUpdate)
            {
                Console.WriteLine("Starting update process...");
                await coreUpdater.RunUpdates(coreName, cleanInstall);
                Pause();
            }
            else if (downloadFirmware)
            {
                await coreUpdater.UpdateFirmware();
            }
            else if (forceInstanceGenerator)
            {
                RunInstanceGenerator(coreUpdater, true);
            }
            else if (downloadAssets != null)
            {
                if (downloadAssets == "all")
                {
                    await coreUpdater.RunAssetDownloader();
                }
                else
                {
                    await coreUpdater.RunAssetDownloader(downloadAssets);
                }
            }
            else if (imagePackOwner != null)
            {
                ImagePack pack = new ImagePack
                {
                    owner = imagePackOwner,
                    repository = imagePackRepo,
                    variant = imagePackVariant
                };

                await pack.Install(path);
            }
            else if (verb == "uninstall")
            {
                if (GlobalHelper.GetCore(coreName) == null)
                {
                    Console.WriteLine("Unknown core");
                }
                else
                {
                    coreUpdater.DeleteCore(GlobalHelper.GetCore(coreName), true, nuke);
                }
            }
            else if (verb == "backup-saves")
            {
                AssetsService.BackupSaves(path, backupSaves_Path);

                if (backupSaves_SaveConfig)
                {
                    var config = GlobalHelper.SettingsManager.GetConfig();

                    config.backup_saves = true;
                    config.backup_saves_location = backupSaves_Path;

                    GlobalHelper.SettingsManager.SaveSettings();
                }
            }
            else
            {
                bool flag = true;

                while (flag)
                {
                    int choice = DisplayMenuNew();

                    switch (choice)
                    {
                        case 1:
                            await coreUpdater.UpdateFirmware();
                            Pause();
                            break;

                        case 2:
                            Console.WriteLine("Checking for required files...");
                            await coreUpdater.RunAssetDownloader();
                            Pause();
                            break;

                        case 3:
                            List<Core> cores = await CoresService.GetCores();
                            AskAboutNewCores(true);
                            RunCoreSelector(cores);
                            // Is reloading the settings file necessary?
                            GlobalHelper.ReloadSettings();
                            break;

                        case 4:
                            await ImagePackSelector(path);
                            break;

                        case 5:
                            RunInstanceGenerator(coreUpdater);
                            Pause();
                            break;

                        case 6:
                            await BuildGameAndWatchRoms(path);
                            Pause();
                            break;

                        case 7:
                            coreUpdater.ForceDisplayModes();
                            Pause();
                            break;

                        case 8:
                            AssetsService.BackupSaves(path, GlobalHelper.SettingsManager.GetConfig().backup_saves_location);
                            Pause();
                            break;

                        case 9:
                            SettingsMenu();

                            coreUpdater.DeleteSkippedCores(GlobalHelper.SettingsManager.GetConfig().delete_skipped_cores);
                            coreUpdater.DownloadFirmware(GlobalHelper.SettingsManager.GetConfig().download_firmware);
                            coreUpdater.DownloadAssets(GlobalHelper.SettingsManager.GetConfig().download_assets);
                            coreUpdater.RenameJotegoCores(GlobalHelper.SettingsManager.GetConfig().fix_jt_names);
                            coreUpdater.BackupSaves(GlobalHelper.SettingsManager.GetConfig().backup_saves, GlobalHelper.SettingsManager.GetConfig().backup_saves_location);
                            // Is reloading the settings file necessary?
                            GlobalHelper.ReloadSettings();
                            break;

                        case 10:
                            flag = false;
                            break;

                        default:
                            Console.WriteLine("Starting update process...");
                            await coreUpdater.RunUpdates();
                            Pause();
                            break;
                    }
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Well, something went wrong. Sorry about that.");
            Console.WriteLine(e);
            Pause();
        }
    }

    private static void coreUpdater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    static void coreUpdater_UpdateProcessComplete(object sender, UpdateProcessCompleteEventArgs e)
    {
        Console.WriteLine("-------------");
        Console.WriteLine(e.Message);

        if (e.InstalledCores != null && e.InstalledCores.Count > 0)
        {
            Console.WriteLine("Cores Updated:");

            foreach (Dictionary<string, string> core in e.InstalledCores)
            {
                Console.WriteLine(core["core"] + " " + core["version"]);
            }

            Console.WriteLine("");
        }

        if (e.InstalledAssets.Count > 0)
        {
            Console.WriteLine("Assets Installed:");

            foreach (string asset in e.InstalledAssets)
            {
                Console.WriteLine(asset);
            }

            Console.WriteLine("");
        }

        if (e.SkippedAssets.Count > 0)
        {
            Console.WriteLine("Assets Not Found:");
            foreach (string asset in e.SkippedAssets)
            {
                Console.WriteLine(asset);
            }

            Console.WriteLine("");
        }

        if (e.FirmwareUpdated != "")
        {
            Console.WriteLine("New Firmware was downloaded. Restart your Pocket to install");
            Console.WriteLine(e.FirmwareUpdated);
            Console.WriteLine("");
        }

        if (e.MissingBetaKeys.Count > 0)
        {
            Console.WriteLine("Missing or incorrect Beta Key for the following cores:");
            foreach (string core in e.MissingBetaKeys)
            {
                Console.WriteLine(core);
            }

            Console.WriteLine("");
        }

        Console.WriteLine(GetRandomSponsorLinks());
        FunFacts();
    }
}
