using System;
using CommandLine;
using Pannella.Helpers;
using Pannella.Models;
using Pannella.Models.Events;
using Pannella.Models.PocketLibraryImages;
using Pannella.Options;
using Pannella.Services;

namespace Pannella;

internal static partial class Program
{
    private static readonly Type[] VerbOptionTypes =
    {
        typeof(MenuOptions), 
        typeof(FundOptions), 
        typeof(UpdateOptions),
        typeof(AssetsOptions), 
        typeof(FirmwareOptions), 
        typeof(ImagesOptions), 
        typeof(InstanceGeneratorOptions),
        typeof(UpdateSelfOptions), 
        typeof(UninstallOptions), 
        typeof(BackupSavesOptions), 
        typeof(GameBoyPalettesOptions),
        typeof(PocketLibraryImagesOptions), 
        typeof(PocketExtrasOptions), 
        typeof(DisplayModesOptions), 
        typeof(PruneMemoriesOptions),
        typeof(AnalogizerSetupOptions),
        typeof(ClearArchiveCacheOptions),
        typeof(ValidateCoresOptions),
    };

    private static void Main(string[] args)
    {
        try
        {
            Parser parser = new Parser(config => config.HelpWriter = null);

            var parserResult = parser.ParseArguments(args, VerbOptionTypes)
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
                                Console.WriteLine("pupdate v" + VERSION);
                                break;
                        }
                    }

                    Environment.Exit(1);
                });

            if (parserResult.Value is BaseOptions baseOptions)
            {
                AssumeYes = baseOptions.AssumeYes;
            }

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

            if (path != null)
            {
                Directory.CreateDirectory(path);
            }

            ServiceHelper.Initialize(path, path, coreUpdater_StatusUpdated, coreUpdater_UpdateProcessComplete);

            bool enableMissingCores = false;

            // The self-update must target the directory containing the running
            // pupdate binary, which is not necessarily the install path (-p) when
            // the binary lives outside the SD card. See issue #452.
            string executableDirectory = Path.GetDirectoryName(Environment.ProcessPath) ?? ServiceHelper.UpdateDirectory;

            switch (parserResult.Value)
            {
                case MenuOptions options:
                    if (!options.SkipUpdate)
                        CheckForUpdates(executableDirectory, false, args, ServiceHelper.SettingsService.Config.auto_install_updates);
                    else
                        enableMissingCores = true;
                    break;

                case UpdateSelfOptions:
                    CheckForUpdates(executableDirectory, true, args, false);
                    // CheckForUpdates will terminate execution when necessary.
                    break;

                case FirmwareOptions:
                    ServiceHelper.FirmwareService.UpdateFirmware(ServiceHelper.UpdateDirectory);
                    return;

                case FundOptions options:
                    Funding(options.Core);
                    return;

                case ValidateCoresOptions options:
                    // Run before CheckForMissingCores: that step reads every core's
                    // JSON, so a corrupt core would throw before we could report it.
                    ValidateCores(options.Fix);
                    return;
            }

            // If we have any missing cores, handle them.
            // If we're in Menu mode, show the core selector.
            // If not, auto enable them.
            CheckForMissingCores(enableMissingCores);

            CoreUpdaterService coreUpdaterService = new CoreUpdaterService(
                ServiceHelper.UpdateDirectory,
                ServiceHelper.CoresService.Cores,
                ServiceHelper.FirmwareService,
                ServiceHelper.SettingsService,
                ServiceHelper.CoresService);

            coreUpdaterService.StatusUpdated += coreUpdater_StatusUpdated;
            coreUpdaterService.UpdateProcessComplete += coreUpdater_UpdateProcessComplete;

            switch (parserResult.Value)
            {
                case UpdateOptions options:
                    Console.WriteLine("Starting update process...");
                    string[] identifiers = null;

                    if (!string.IsNullOrEmpty(options.CoreName))
                    {
                        identifiers = new[] { options.CoreName };
                    }

                    int updateErrors = coreUpdaterService.RunUpdates(identifiers, options.CleanInstall,
                        options.UpdatedAssetsOnly);

                    if (updateErrors > 0)
                    {
                        Environment.ExitCode = 1;
                    }

                    break;

                case InstanceGeneratorOptions:
                    RunInstanceGenerator(coreUpdaterService, true);
                    break;

                case ImagesOptions options:
                    ServiceHelper.PlatformImagePacksService.Install(options.ImagePackOwner, options.ImagePackRepo,
                        options.ImagePackVariant);
                    break;

                case AssetsOptions options:
                    var cores = ServiceHelper.CoresService.Cores
                        .Where(core => !string.IsNullOrEmpty(options.CoreName) || core.id == options.CoreName)
                        .Where(core => !ServiceHelper.SettingsService.GetCoreSettings(core.id).skip)
                        .ToList();

                    ServiceHelper.CoresService.DownloadCoreAssets(cores);
                    break;

                case UninstallOptions options when ServiceHelper.CoresService.GetCore(options.CoreName) == null:
                    Console.WriteLine($"Unknown core '{options.CoreName}'");
                    break;

                case UninstallOptions options:
                    coreUpdaterService.DeleteCore(ServiceHelper.CoresService.GetCore(options.CoreName), true,
                        options.DeleteAssets);
                    break;

                case BackupSavesOptions options:
                    AssetsService.BackupSaves(ServiceHelper.UpdateDirectory, options.BackupPath);
                    AssetsService.BackupMemories(ServiceHelper.UpdateDirectory, options.BackupPath);

                    if (options.Save)
                    {
                        var config = ServiceHelper.SettingsService.Config;

                        config.backup_saves = true;
                        config.backup_saves_location = options.BackupPath;

                        ServiceHelper.SettingsService.Save();
                    }

                    break;

                case GameBoyPalettesOptions:
                    DownloadGameBoyPalettes();
                    break;

                case PocketLibraryImagesOptions options:
                    if (options.List)
                    {
                        Console.WriteLine();
                        Console.WriteLine(
                            "Spiritualized: run pocket-library-images with no -n to install from your configured archive.");
                        Console.WriteLine();

                        foreach (PocketLibraryImageMenu menu in ServiceHelper.CoresService.PocketLibraryImagesList)
                        {
                            Console.WriteLine($"{menu.menu_title}");
                            foreach (PocketLibraryImage image in menu.entries)
                            {
                                PrintPocketLibraryImageInfo(image);
                            }
                        }
                    }
                    else if (!string.IsNullOrEmpty(options.Name))
                    {
                        PocketLibraryImage image = ServiceHelper.CoresService.GetPocketLibraryImage(options.Name);

                        if (image != null)
                        {
                            if (options.Info)
                            {
                                Console.WriteLine();
                                PrintPocketLibraryImageInfo(image);
                            }
                            else
                            {
                                ServiceHelper.CoresService.DownloadPocketLibraryImages(image);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Pocket library image '{options.Name}' not found.");
                        }
                    }
                    else
                    {
                        ServiceHelper.CoresService.DownloadPockLibraryImages();
                    }

                    break;

                case PocketExtrasOptions options:
                    if (options.List)
                    {
                        Console.WriteLine();

                        foreach (var extra in ServiceHelper.CoresService.PocketExtrasList)
                        {
                            PrintPocketExtraInfo(extra);
                        }
                    }
                    else if (!string.IsNullOrEmpty(options.Name))
                    {
                        var extra = ServiceHelper.CoresService.GetPocketExtra(options.Name); // pocket extras id

                        if (extra != null)
                        {
                            if (options.Info)
                            {
                                Console.WriteLine();
                                PrintPocketExtraInfo(extra);
                            }
                            else
                            {
                                ServiceHelper.CoresService.GetPocketExtra(extra, ServiceHelper.UpdateDirectory, true);
                            }
                        }
                        else
                        {
                            Console.WriteLine($"Pocket Extra '{options.Name}' not found.");
                        }
                    }
                    else
                    {
                        Console.WriteLine("Missing required parameter: -n or --name");
                    }

                    break;

                case DisplayModesOptions:
                    EnableDisplayModes(isCurated: true);
                    break;

                case PruneMemoriesOptions options:
                    AssetsService.PruneSaveStates(ServiceHelper.UpdateDirectory, options.CoreName);
                    break;

                case AnalogizerSetupOptions options:
                    if (options.Jotego)
                    {
                        JotegoAnalogizerSettingsService settings = new JotegoAnalogizerSettingsService();
                        settings.RunAnalogizerSettings();
                        Console.WriteLine("Jotego Analogizer configuration updated.");
                    }
                    else 
                    {
                        AnalogizerSettingsService.ShowWizard();
                    }
                    break;

                case ClearArchiveCacheOptions options:
                    if (!options.AssumeYes)
                    {
                        Console.WriteLine("Specify -y or --yes to confirm clearing the archive cache.");
                        break;
                    }

                    ClearArchiveCache(promptForConfirmation: false);
                    break;

                default:
                    DisplayMenu(coreUpdaterService);
                    break;
            }
        }
        catch (Exception ex)
        {
            // Set the failure exit code first: anything below could itself throw,
            // and unattended callers rely on a non-zero code to detect failure.
            Environment.ExitCode = 1;

            Console.WriteLine("Well, something went wrong. Sorry about that.");

            // SettingsService may be null if we failed before initialization
            // (e.g. the install path could not be created), so guard the lookup.
            bool showStackTraces = ServiceHelper.SettingsService?.Debug?.show_stack_traces ?? false;

            Console.WriteLine(showStackTraces
                ? ex.ToString()
                : Util.GetExceptionMessage(ex));

            // Don't block waiting for a keypress in unattended runs: there's no
            // one to press it, and Console.ReadKey throws on redirected stdin.
            if (!AssumeYes && !Console.IsInputRedirected)
            {
                Pause();
            }
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

        if (e.InstalledCores is { Count: > 0 })
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

        if (e.MissingLicenses.Count > 0)
        {
            Console.WriteLine("Missing or incorrect License file for the following cores:");

            foreach (string core in e.MissingLicenses)
            {
                Console.WriteLine(core);
            }

            Console.WriteLine();
        }

        if (e.ErrorCount > 0)
        {
            Console.WriteLine($"{e.ErrorCount} core(s) failed to update. See the log above for details.");
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
