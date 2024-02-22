using CommandLine;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Options;
using Pannella.Services;

namespace Pannella;

internal partial class Program
{
    private static void Main(string[] args)
    {
        try
        {
            #region Command Line Arguments

            Parser parser = new Parser(config => config.HelpWriter = null);

            var parserResult = parser.ParseArguments<MenuOptions, FundOptions, UpdateOptions,
                    AssetsOptions, FirmwareOptions, ImagesOptions, InstanceGeneratorOptions,
                    UpdateSelfOptions, UninstallOptions, BackupSavesOptions, GameBoyPalettesOptions,
                    PocketLibraryImagesOptions, PocketExtrasOptions>(args)
                .WithNotParsed(errors =>
                {
                    foreach (var error in errors)
                    {
                        switch (error)
                        {
                            case MissingRequiredOptionError mro:
                                Console.WriteLine($"Missing required parameter: -{mro.NameInfo.ShortName} or --{mro.NameInfo.LongName}.");
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

            string path;

            if (parserResult.Value is UpdateSelfOptions ||
                string.IsNullOrEmpty(((BaseOptions)parserResult.Value).InstallPath))
            {
                path = Path.GetDirectoryName(Environment.ProcessPath);
            }
            else
            {
                path = ((BaseOptions)parserResult.Value).InstallPath;
            }

            #endregion

            GlobalHelper.Initialize(path, coreUpdater_StatusUpdated, coreUpdater_UpdateProcessComplete);

            bool enableMissingCores = false;

            switch (parserResult.Value)
            {
                case MenuOptions options:
                    if (!options.SkipUpdate)
                        CheckForUpdates(GlobalHelper.UpdateDirectory, false, args);
                    else
                        enableMissingCores = true;
                    break;

                case UpdateSelfOptions:
                    CheckForUpdates(GlobalHelper.UpdateDirectory, true, args);
                    // CheckForUpdates will terminate execution when necessary.
                    break;

                case FirmwareOptions:
                    GlobalHelper.FirmwareService.UpdateFirmware(GlobalHelper.UpdateDirectory);
                    return;

                case FundOptions options:
                    Funding(options.Core);
                    return;
            }

            // If we have any missing cores, handle them.
            // If we're in Menu mode, show the core selector.
            // If not, auto enable them.
            CheckForMissingCores(enableMissingCores);

            CoreUpdaterService coreUpdaterService = new CoreUpdaterService(
                GlobalHelper.UpdateDirectory,
                GlobalHelper.CoresService.Cores,
                GlobalHelper.FirmwareService,
                GlobalHelper.JotegoService,
                GlobalHelper.PocketExtrasService,
                GlobalHelper.SettingsService);

            coreUpdaterService.StatusUpdated += coreUpdater_StatusUpdated;
            coreUpdaterService.UpdateProcessComplete += coreUpdater_UpdateProcessComplete;

            switch (parserResult.Value)
            {
                case UpdateOptions options:
                    Console.WriteLine("Starting update process...");
                    coreUpdaterService.RunUpdates(options.CoreName, options.CleanInstall);
                    Pause();
                    break;

                case InstanceGeneratorOptions:
                    RunInstanceGenerator(coreUpdaterService, true);
                    break;

                case ImagesOptions options:
                    GlobalHelper.PlatformImagePacksService.Install(options.ImagePackOwner, options.ImagePackRepo,
                        options.ImagePackVariant);
                    break;

                case AssetsOptions options:
                    var cores = GlobalHelper.CoresService.Cores
                        .Where(core => !string.IsNullOrEmpty(options.CoreName) || core.identifier == options.CoreName)
                        .Where(core => !GlobalHelper.SettingsService.GetCoreSettings(core.identifier).skip)
                        .ToList();

                    GlobalHelper.CoresService.DownloadCoreAssets(cores);
                    break;

                case UninstallOptions options when CoresService.GetCore(options.CoreName) == null:
                    Console.WriteLine($"Unknown core '{options.CoreName}'");
                    break;

                case UninstallOptions options:
                    coreUpdaterService.DeleteCore(CoresService.GetCore(options.CoreName), true, options.DeleteAssets);
                    break;

                case BackupSavesOptions options:
                    AssetsService.BackupSaves(GlobalHelper.UpdateDirectory, options.BackupPath);
                    AssetsService.BackupMemories(GlobalHelper.UpdateDirectory, options.BackupPath);

                    if (options.Save)
                    {
                        var config = GlobalHelper.SettingsService.GetConfig();

                        config.backup_saves = true;
                        config.backup_saves_location = options.BackupPath;

                        GlobalHelper.SettingsService.Save();
                    }

                    break;

                case GameBoyPalettesOptions:
                    DownloadGameBoyPalettes();
                    break;

                case PocketLibraryImagesOptions:
                    DownloadPockLibraryImages();
                    break;

                case PocketExtrasOptions options:
                    if (options.List)
                    {
                        Console.WriteLine();

                        foreach (var extra in GlobalHelper.PocketExtrasService.List)
                        {
                            PrintPocketExtraInfo(extra);
                        }
                    }
                    else if (!string.IsNullOrEmpty(options.Name))
                    {
                        var extra = GlobalHelper.PocketExtrasService.GetPocketExtra(options.Name);

                        if (extra != null)
                        {
                            if (options.Info)
                            {
                                Console.WriteLine();
                                PrintPocketExtraInfo(extra);
                            }
                            else
                            {
                                GlobalHelper.PocketExtrasService.GetPocketExtra(extra, GlobalHelper.UpdateDirectory, true);
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
                    DisplayMenuNew(coreUpdaterService);
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

    private static void coreUpdater_StatusUpdated(object sender, StatusUpdatedEventArgs e)
    {
        Console.WriteLine(e.Message);
    }

    private static void coreUpdater_UpdateProcessComplete(object sender, UpdateProcessCompleteEventArgs e)
    {
        Console.WriteLine(Base.DIVIDER);
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

        if (!string.IsNullOrEmpty(e.FirmwareUpdated))
        {
            Console.WriteLine("New Firmware was downloaded. Restart your Pocket to install.");
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
