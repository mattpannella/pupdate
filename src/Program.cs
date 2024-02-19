using CommandLine;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Extras;
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
            string path = null;
            bool preservePlatformsFolder = false;
            string downloadAssets = null;
            string coreName = null;
            string imagePackOwner = null;
            string imagePackRepo = null;
            string imagePackVariant = null;
            bool selfUpdate = false;
            bool nuke = false;
            bool cleanInstall = false;
            string backupSaves_Path = null;
            bool backupSaves_SaveConfig = false;
            string pocket_extras_name = null;
            bool pocket_extras_list = false;
            bool pocket_extras_info = false;

            string verb = "menu";
            Dictionary<string, object> data = new Dictionary<string, object>();

            #region Command Line Arguments

            Parser parser = new Parser(config => config.HelpWriter = null);

            var parserResult = parser.ParseArguments<MenuOptions, FundOptions, UpdateOptions,
                    AssetsOptions, FirmwareOptions, ImagesOptions, InstanceGeneratorOptions,
                    UpdateSelfOptions, UninstallOptions, BackupSavesOptions, GameBoyPalettesOptions,
                    PocketLibraryImagesOptions, PocketExtrasOptions>(args)
                .WithParsed<UpdateSelfOptions>(_ => { selfUpdate = true; })
                .WithParsed<FundOptions>(fundOptions =>
                {
                    path = fundOptions.InstallPath;
                })
                .WithParsed<UpdateOptions>(o =>
                {
                    verb = "update";
                    CLI_MODE = true;
                    path = o.InstallPath;

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
                    path = o.InstallPath;

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
                    path = o.InstallPath;

                    if (o.CoreName != null)
                    {
                        downloadAssets = o.CoreName;
                    }
                })
                .WithParsed<FirmwareOptions>(o =>
                {
                    verb = "firmware";
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<ImagesOptions>(o =>
                {
                    verb = "images";
                    CLI_MODE = true;
                    path = o.InstallPath;

                    if (o.ImagePackOwner != null)
                    {
                        imagePackOwner = o.ImagePackOwner;
                        imagePackRepo = o.ImagePackRepo;
                        imagePackVariant = o.ImagePackVariant;
                    }
                })
                .WithParsed<InstanceGeneratorOptions>(o =>
                {
                    verb = "instance-generator";
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<MenuOptions>(o =>
                {
                    path = o.InstallPath;

                    if (o.SkipUpdate)
                    {
                        CLI_MODE = true;
                    }
                })
                .WithParsed<BackupSavesOptions>(o =>
                {
                    verb = "backup-saves";
                    CLI_MODE = true;
                    backupSaves_Path = o.BackupPath;
                    backupSaves_SaveConfig = o.Save;
                    path = o.InstallPath;
                })
                .WithParsed<GameBoyPalettesOptions>(o =>
                {
                    verb = "gameboy-palettes";
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<PocketLibraryImagesOptions>(o =>
                {
                    verb = "pocket-library-images";
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<PocketExtrasOptions>(o =>
                {
                    verb = "pocket-extras";
                    CLI_MODE = true;
                    pocket_extras_name = o.Name;
                    pocket_extras_list = o.List;
                    pocket_extras_info = o.Info;
                    path = o.InstallPath;
                })
                .WithNotParsed(errors =>
                {
                    foreach (var error in errors)
                    {
                        switch (error)
                        {
                            case MissingRequiredOptionError mro:
                                Console.WriteLine(
                                    $"Missing required parameter: -{mro.NameInfo.ShortName} or --{mro.NameInfo.LongName}.");
                                break;

                            case HelpRequestedError:
                            case HelpVerbRequestedError:
                                Console.WriteLine(HELP_TEXT);
                                break;

                            case VersionRequestedError:
                                Console.WriteLine("Pupdate v" + VERSION);
                                break;
                        }
                    }

                    Environment.Exit(1);
                });

            if (string.IsNullOrEmpty(path))
            {
                path = Path.GetDirectoryName(Environment.ProcessPath);
            }

            #endregion

            await GlobalHelper.Initialize(path);
            GlobalHelper.PocketExtrasService.StatusUpdated += coreUpdater_StatusUpdated;
            GlobalHelper.PocketExtrasService.UpdateProcessComplete += coreUpdater_UpdateProcessComplete;

            switch (parserResult.Value)
            {
                case MenuOptions:
                case UpdateSelfOptions:
                    await CheckForUpdates(path, selfUpdate, args);
                    break;

                case FundOptions fundOptions:
                    Funding(fundOptions.Core);
                    Environment.Exit(1);
                    break;
            }

            PocketCoreUpdater coreUpdater = new PocketCoreUpdater();

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
            coreUpdater.BackupSaves(GlobalHelper.SettingsManager.GetConfig().backup_saves,
                GlobalHelper.SettingsManager.GetConfig().backup_saves_location);

            // If we have any missing cores, handle them.
            CheckForMissingCores(CLI_MODE);

            switch (verb)
            {
                case "update":
                    Console.WriteLine("Starting update process...");
                    await coreUpdater.RunUpdates(coreName, cleanInstall);
                    Pause();
                    break;

                case "firmware":
                    await coreUpdater.UpdateFirmware();
                    break;

                case "instance-generator":
                    RunInstanceGenerator(coreUpdater, true);
                    break;

                case "images":
                    ImagePack pack = new ImagePack
                    {
                        owner = imagePackOwner,
                        repository = imagePackRepo,
                        variant = imagePackVariant
                    };

                    await pack.Install(path);
                    break;

                case "assets":
                    if (downloadAssets == "all")
                        await coreUpdater.RunAssetDownloader();
                    else
                        await coreUpdater.RunAssetDownloader(downloadAssets);

                    break;

                case "uninstall" when GlobalHelper.GetCore(coreName) == null:
                    Console.WriteLine($"Unknown core '{coreName}'");
                    break;

                case "uninstall":
                    coreUpdater.DeleteCore(GlobalHelper.GetCore(coreName), true, nuke);
                    break;

                case "backup-saves":
                    AssetsService.BackupSaves(path, backupSaves_Path);
                    AssetsService.BackupMemories(path, backupSaves_Path);

                    if (backupSaves_SaveConfig)
                    {
                        var config = GlobalHelper.SettingsManager.GetConfig();

                        config.backup_saves = true;
                        config.backup_saves_location = backupSaves_Path;

                        GlobalHelper.SettingsManager.SaveSettings();
                    }

                    break;

                case "gameboy-palettes":
                    await DownloadGameBoyPalettes(path);
                    break;

                case "pocket-library-images":
                    await DownloadPockLibraryImages(path);
                    break;

                case "pocket-extras":
                    if (pocket_extras_list)
                    {
                        Console.WriteLine();

                        foreach (var extra in GlobalHelper.PocketExtras)
                        {
                            PrintPocketExtraInfo(extra);
                        }
                    }
                    else if (!string.IsNullOrEmpty(pocket_extras_name))
                    {
                        var extra = GlobalHelper.GetPocketExtra(pocket_extras_name);

                        if (extra != null)
                        {
                            if (pocket_extras_info)
                            {
                                Console.WriteLine();
                                PrintPocketExtraInfo(extra);
                            }
                            else
                            {
                                await GlobalHelper.PocketExtrasService.GetPocketExtra(extra, path, true, true);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Pocket Extra '{pocket_extras_name}' not found.");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Missing required parameter: -n or --name");
                    }

                    break;

                default:
                    DisplayMenuNew(path, coreUpdater);
                    break;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine("Well, something went wrong. Sorry about that.");
#if DEBUG
            Console.WriteLine(e);
#else
            Console.WriteLine(e.Message);
#endif
            Pause();
        }
    }

    private static void PrintPocketExtraInfo(PocketExtra extra)
    {
        Console.WriteLine(extra.id);
        Console.WriteLine(string.IsNullOrEmpty(extra.name)
            ? $"  {extra.core_identifiers[0]}"
            : $"  {extra.name}");
        Console.WriteLine(Util.WordWrap(extra.description, 80, "    "));
        Console.WriteLine($"    More info: https://github.com/{extra.github_user}/{extra.github_repository}");

        foreach (var additionalLink in extra.additional_links)
        {
            Console.WriteLine($"                {additionalLink}");
        }

        Console.WriteLine();
    }

    private static void coreUpdater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    private static void coreUpdater_UpdateProcessComplete(object sender, UpdateProcessCompleteEventArgs e)
    {
        Console.WriteLine("-------------");
        Console.WriteLine(e.Message);

        if (e.InstalledCores != null && e.InstalledCores.Count > 0)
        {
            Console.WriteLine("Cores Updated:");

            foreach (Dictionary<string, string> core in e.InstalledCores)
            {
                Console.WriteLine($"{core["core"]} {core["version"]}");
            }

            Console.WriteLine();
        }

        if (e.InstalledAssets.Count > 0)
        {
            Console.WriteLine("Assets Installed:");

            foreach (string asset in e.InstalledAssets)
            {
                Console.WriteLine(asset);
            }

            Console.WriteLine();
        }

        if (e.SkippedAssets.Count > 0)
        {
            Console.WriteLine("Assets Not Found:");
            foreach (string asset in e.SkippedAssets)
            {
                Console.WriteLine(asset);
            }

            Console.WriteLine();
        }

        if (e.FirmwareUpdated != string.Empty)
        {
            Console.WriteLine("New Firmware was downloaded. Restart your Pocket to install");
            Console.WriteLine(e.FirmwareUpdated);
            Console.WriteLine();
        }

        if (e.MissingBetaKeys.Count > 0)
        {
            Console.WriteLine("Missing or incorrect Beta Key for the following cores:");
            foreach (string core in e.MissingBetaKeys)
            {
                Console.WriteLine(core);
            }

            Console.WriteLine();
        }

        if (!e.SkipOutro)
        {
            var links = GetRandomSponsorLinks();

            if (!string.IsNullOrEmpty(links))
            {
                Console.WriteLine();
                Console.WriteLine(links);
            }

            FunFacts();
        }
    }
}
