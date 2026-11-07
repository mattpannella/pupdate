using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella.Helpers;

public enum FirstRunChoice
{
    KeepInstalled,
    ByType,
    None,
    Everything
}

/// <summary>
/// The first-run core choice: hundreds of cores are summarised by type (the inventory's platform
/// category) instead of being listed one by one.
/// </summary>
public static class FirstRunCoreSelection
{
    public const string OTHER_TYPE = "Other";

    // Shown in this order; every other category is folded into "Other".
    private static readonly string[] MAIN_TYPES = { "Console", "Handheld", "Computer", "Arcade", "Arcade Multi" };

    public static string TypeOf(Core core)
    {
        string category = core?.platform?.category;

        return MAIN_TYPES.FirstOrDefault(t => string.Equals(t, category, StringComparison.OrdinalIgnoreCase))
               ?? OTHER_TYPE;
    }

    public static List<(string Type, int Count)> Summarize(IEnumerable<Core> cores)
    {
        var counts = cores.GroupBy(TypeOf).ToDictionary(g => g.Key, g => g.Count());

        return MAIN_TYPES.Append(OTHER_TYPE)
            .Where(counts.ContainsKey)
            .Select(t => (t, counts[t]))
            .ToList();
    }

    public static bool ShouldEnable(Core core, FirstRunChoice choice, Func<string, bool> isInstalled,
        ICollection<string> types = null)
    {
        return choice switch
        {
            FirstRunChoice.Everything => true,
            FirstRunChoice.KeepInstalled => isInstalled(core.id),
            FirstRunChoice.ByType => types != null && types.Contains(TypeOf(core)),
            _ => false
        };
    }
}
