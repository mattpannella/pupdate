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
            bool selfUpdate = false;

            #region Command Line Arguments

            Parser parser = new Parser(config => config.HelpWriter = null);

            var parserResult = parser.ParseArguments<MenuOptions, FundOptions, UpdateOptions,
                    AssetsOptions, FirmwareOptions, ImagesOptions, InstanceGeneratorOptions,
                    UpdateSelfOptions, UninstallOptions, BackupSavesOptions, GameBoyPalettesOptions,
                    PocketLibraryImagesOptions, PocketExtrasOptions>(args)
                .WithParsed<UpdateSelfOptions>(_ => { selfUpdate = true; })
                .WithParsed<FundOptions>(o =>
                {
                    path = o.InstallPath;
                })
                .WithParsed<UpdateOptions>(o =>
                {
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<UninstallOptions>(o =>
                {
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<AssetsOptions>(o =>
                {
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<FirmwareOptions>(o =>
                {
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<ImagesOptions>(o =>
                {
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<InstanceGeneratorOptions>(o =>
                {
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
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<GameBoyPalettesOptions>(o =>
                {
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<PocketLibraryImagesOptions>(o =>
                {
                    CLI_MODE = true;
                    path = o.InstallPath;
                })
                .WithParsed<PocketExtrasOptions>(o =>
                {
                    CLI_MODE = true;
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

                case FundOptions options:
                    Funding(options.Core);
                    Environment.Exit(1);
                    break;
            }

            PocketCoreUpdater coreUpdater = new PocketCoreUpdater();

            if (GlobalHelper.SettingsManager.GetConfig().preserve_platforms_folder)
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

            switch (parserResult.Value)
            {
                case UpdateOptions options:
                    Console.WriteLine("Starting update process...");
                    await coreUpdater.RunUpdates(options.CoreName, options.CleanInstall);
                    Pause();
                    break;

                case FirmwareOptions:
                    await coreUpdater.UpdateFirmware();
                    break;

                case InstanceGeneratorOptions:
                    RunInstanceGenerator(coreUpdater, true);
                    break;

                case ImagesOptions options:
                    ImagePack pack = new ImagePack
                    {
                        owner = options.ImagePackOwner,
                        repository = options.ImagePackRepo,
                        variant = options.ImagePackVariant
                    };

                    await pack.Install(path);
                    break;

                case AssetsOptions options:
                    // can likely just use the option value without the check
                    if (string.IsNullOrEmpty(options.CoreName))
                        await coreUpdater.RunAssetDownloader();
                    else
                        await coreUpdater.RunAssetDownloader(options.CoreName);

                    break;

                case UninstallOptions options when GlobalHelper.GetCore(options.CoreName) == null:
                    Console.WriteLine($"Unknown core '{options.CoreName}'");
                    break;

                case UninstallOptions options:
                    coreUpdater.DeleteCore(GlobalHelper.GetCore(options.CoreName), true, options.DeleteAssets);
                    break;

                case BackupSavesOptions options:
                    AssetsService.BackupSaves(path, options.BackupPath);
                    AssetsService.BackupMemories(path, options.BackupPath);

                    if (options.Save)
                    {
                        var config = GlobalHelper.SettingsManager.GetConfig();

                        config.backup_saves = true;
                        config.backup_saves_location = options.BackupPath;

                        GlobalHelper.SettingsManager.SaveSettings();
                    }

                    break;

                case GameBoyPalettesOptions:
                    await DownloadGameBoyPalettes(path);
                    break;

                case PocketLibraryImagesOptions:
                    await DownloadPockLibraryImages(path);
                    break;

                case PocketExtrasOptions options:
                    if (options.List)
                    {
                        Console.WriteLine();

                        foreach (var extra in GlobalHelper.PocketExtras)
                        {
                            PrintPocketExtraInfo(extra);
                        }
                    }
                    else if (!string.IsNullOrEmpty(options.Name))
                    {
                        var extra = GlobalHelper.GetPocketExtra(options.Name);

                        if (extra != null)
                        {
                            if (options.Info)
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
                            Console.WriteLine($"Pocket Extra '{options.Name}' not found.");
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
