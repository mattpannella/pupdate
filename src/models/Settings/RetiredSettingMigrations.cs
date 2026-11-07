namespace Pannella.Models.Settings;

/// <summary>
/// Features that moved out of pupdate into plugins. The ids are plugin catalog ids (plugins.json).
/// Config.Migrate() queues one of these when the retired setting that drove the feature was on, and
/// the one-time notice at startup offers to install the plugin.
/// </summary>
public sealed record RetiredSettingMigration(string PluginId, string Feature, string Message,
    string RequiresInstalledCorePrefix = null);

public static class RetiredSettingMigrations
{
    public const string COINOP_LICENSE = "coinop-license";
    public const string JOTEGO_PLATFORM_NAMES = "jotego-platform-names";
    public const string JOTEGO_BETA_KEY = "jotego-beta-key";

    public static readonly IReadOnlyList<RetiredSettingMigration> All = new[]
    {
        new RetiredSettingMigration(COINOP_LICENSE, "Coin-Op Collection license",
            "Fetching the Coin-Op Collection license is now done by the Coin-Op License plugin. " +
            "It reads the same <serial>.ID file and runs before the cores during Update All."),
        new RetiredSettingMigration(JOTEGO_PLATFORM_NAMES, "Jotego platform names",
            "Renaming Jotego platforms to their friendly names is now done by the Jotego Platform Names plugin. " +
            "It runs after the cores during Update All.",
            RequiresInstalledCorePrefix: "jotego."),
        new RetiredSettingMigration(JOTEGO_BETA_KEY, "Jotego beta key",
            "Fetching jtbeta.zip from GitHub or Patreon is now done by the Jotego Beta Key plugin. " +
            "Your token or session cookie must be entered again in the plugin the first time it runs. " +
            "Placing jtbeta.zip on the SD card root still works without it."),
    };

    public static RetiredSettingMigration Find(string pluginId)
    {
        return All.FirstOrDefault(m => string.Equals(m.PluginId, pluginId, StringComparison.OrdinalIgnoreCase));
    }
}
