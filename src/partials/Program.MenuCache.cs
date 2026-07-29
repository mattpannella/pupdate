using Pannella.Helpers;
using Pannella.Options;
using Pannella.Services;

namespace Pannella;

internal static partial class Program
{
    private static void RunMenuCache(MenuCacheOptions options)
    {
        var service = new MenuCacheService(ServiceHelper.UpdateDirectory, ServiceHelper.CoresService);

        Console.WriteLine("openFPGA menu cache (EXPERIMENTAL)");
        Console.WriteLine($"System folder: {service.SystemDirectory}");
        Console.WriteLine();

        var model = service.BuildModel();
        int cores = model.Cores.Count;
        int installed = model.Platforms.Count(p => p.Installed);
        int coreViewSize = service.ProjectedCoreViewSize();
        bool overLimit = coreViewSize > MenuCacheService.BufferSize;

        Console.WriteLine($"Installed platforms: {installed}    Cores: {cores}");
        Console.WriteLine($"  core_viewby_platform.bin (the file that crashes) would be {coreViewSize:n0} of " +
                          $"{MenuCacheService.BufferSize:n0} bytes");
        Console.WriteLine(overLimit
            ? "  *** OVER LIMIT: exceeds the 32 KB buffer. The Pocket rebuilds this view on boot and crashes " +
              "(QR screen). This can't be pre-built around - archive unused platforms to get under the limit."
            : "  OK: fits within the buffer.");
        Console.WriteLine();

        Console.WriteLine("Existing /System cache files:");

        foreach (var file in service.AnalyzeExisting())
        {
            if (!file.Exists)
            {
                Console.WriteLine($"  {file.Name,-30} (not found)");
                continue;
            }

            string records = file.RecordCount.HasValue ? $"{file.RecordCount} records" : string.Empty;
            string flag = file.IsView && file.Overflow ? "  OVER 32KB" : string.Empty;

            Console.WriteLine($"  {file.Name,-30} {file.Size,8:n0} bytes  {records,-14}{flag}");
        }

        Console.WriteLine();

        if (options.WriteSystem)
        {
            if (overLimit)
            {
                Console.WriteLine("Refusing to write: the openFPGA menu is over the Pocket's 32 KB limit, so the Pocket");
                Console.WriteLine("would rebuild it on boot and crash regardless. Archive unused platforms first.");
                Console.WriteLine();

                return;
            }

            Console.WriteLine("Writing full cache set into /System (existing files backed up to .pupdate.bak):");

            foreach (var (name, bytes, backedUp) in service.ApplyToSystem())
            {
                Console.WriteLine($"  {name,-30} {bytes,8:n0} bytes{(backedUp ? "   (backed up)" : "")}");
            }

            Console.WriteLine("Done. Reboot the Pocket - the menu loads without rebuilding.");
            Console.WriteLine();

            return;
        }

        if (options.Verify)
        {
            Console.WriteLine("Verify (regenerate and byte-compare against /System):");

            foreach (var (name, bytes) in service.BuildAll())
            {
                var result = service.Verify(name, bytes);

                if (!result.ExistingFound)
                {
                    Console.WriteLine($"  {name,-30} (no existing file to compare)");
                }
                else if (result.Match)
                {
                    Console.WriteLine($"  {name,-30} MATCH ({result.Generated.Length:n0} bytes)");
                }
                else
                {
                    Console.WriteLine($"  {name,-30} DIFFERS (gen {result.Generated.Length:n0} / " +
                                      $"existing {result.ExistingLength:n0} bytes, first diff @ 0x{result.FirstDifferenceOffset:x})");
                }
            }

            Console.WriteLine();
        }

        if (!string.IsNullOrEmpty(options.Output))
        {
            Directory.CreateDirectory(options.Output);

            foreach (var (name, bytes) in service.BuildAll())
            {
                File.WriteAllBytes(Path.Combine(options.Output, name), bytes);
                Console.WriteLine($"  wrote {name,-30} {bytes.Length,8:n0} bytes");
            }

            Console.WriteLine($"Generated full cache set to {options.Output}.");
            Console.WriteLine("NOTE: back up your Pocket's /System folder before copying any generated cache onto it.");
        }
    }
}
