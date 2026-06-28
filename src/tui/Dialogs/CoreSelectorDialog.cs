using System.Collections.Generic;
using System.Linq;
using Pannella.Helpers;
using Pannella.Models.OpenFPGA_Cores_Inventory.V3;

namespace Pannella.TUI;

/// <summary>
/// Core-selection dialogs, built on the shared <see cref="ChecklistDialog"/>.
///   • Show         — enable/disable: pre-checks enabled cores, returns id-&gt;wanted map.
///   • SelectSubset — pick a subset to act on: starts unchecked, returns the chosen ids.
/// Both return null when cancelled.
/// </summary>
public static class CoreSelectorDialog
{
    public static Dictionary<string, bool> Show(IReadOnlyList<Core> cores, string message)
    {
        var marked = ChecklistDialog.Show("Select Cores", message, Labels(cores),
            i => !ServiceHelper.SettingsService.GetCoreSettings(cores[i].id).skip, "Save",
            categories: PlatformKeys(cores), categoryDisplay: PlatformDisplay(cores), categoryLabel: "Platform");

        if (marked == null)
        {
            return null;
        }

        var result = new Dictionary<string, bool>();

        for (int i = 0; i < cores.Count; i++)
        {
            result[cores[i].id] = marked.Contains(i);
        }

        return result;
    }

    public static List<string> SelectSubset(IReadOnlyList<Core> cores, string message)
    {
        var marked = ChecklistDialog.Show("Select Cores", message, Labels(cores), _ => false, "OK",
            categories: PlatformKeys(cores), categoryDisplay: PlatformDisplay(cores), categoryLabel: "Platform");

        return marked?.Select(i => cores[i].id).ToList();
    }

    private static List<string> Labels(IReadOnlyList<Core> cores) =>
        cores.Select(c => c.requires_license ? $"{c.id}  (license required)" : c.id).ToList();

    // Filter key per core: the platform_id (precise). "(none)" for cores without one.
    private static List<string> PlatformKeys(IReadOnlyList<Core> cores) =>
        cores.Select(c => string.IsNullOrEmpty(c.platform_id) ? "(none)" : c.platform_id).ToList();

    // Maps a platform_id to its friendly platform name for the dropdown (falls back to the id).
    private static Func<string, string> PlatformDisplay(IReadOnlyList<Core> cores)
    {
        var names = cores
            .Where(c => !string.IsNullOrEmpty(c.platform_id) && !string.IsNullOrWhiteSpace(c.platform?.name))
            .GroupBy(c => c.platform_id)
            .ToDictionary(g => g.Key, g => g.First().platform.name);

        return id => names.TryGetValue(id, out var name) ? name : id;
    }
}
