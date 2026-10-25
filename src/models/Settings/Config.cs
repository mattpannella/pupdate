// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable InconsistentNaming

using System.ComponentModel;
using Newtonsoft.Json;

namespace Pannella.Models.Settings;

public class Config
{
    [Description("Download Assets (ROMs and BIOS Files) during 'Update All'")]
    public bool download_assets { get; set; } = true;

    [Description("Only check assets for cores updated during 'Update All'")]
    public bool only_check_updated_core_assets { get; set; } = false;

    public string github_token { get; set; } = string.Empty;

    [Description("Download Firmware Updates during 'Update All'")]
    public bool download_firmware { get; set; } = true;

    [Description("Preserve 'Platforms' folder during 'Update All'")]
    public bool preserve_platforms_folder { get; set; } = false;

    [Description("Delete cores, not managed by pupdate, during 'Update All'")]
    public bool delete_skipped_cores { get; set; } = false;

    public string download_new_cores { get; set; }

    public string display_modes_option { get; set; }

    [Description("Build game JSON files for supported cores during 'Update All'")]
    public bool build_instance_jsons { get; set; } = true;

    [Description("Use CRC check when checking ROMs and BIOS files")]
    public bool crc_check { get; set; } = true;

    [Description("Skip alternative roms when downloading assets")]
    public bool skip_alternative_assets { get; set; } = true;

    [Description("Compress and backup Saves and Memories directories during 'Update All'")]
    public bool backup_saves { get; set; }

    public string backup_saves_location { get; set; } = "Backups";

    public int save_states_to_keep { get; set; } = 1;

    [Description("Cache downloaded archive files locally to avoid re-downloading")]
    public bool cache_archive_files { get; set; } = false;

    public string archive_cache_location { get; set; } = null;

    [Description("Show descriptions for advanced menu items")]
    public bool show_menu_descriptions { get; set; } = true;

    [Description("Use custom asset archive")]
    public bool use_custom_archive { get; set; } = false;

    [Description("Automatically install updates to Pupdate")]
    public bool auto_install_updates { get; set; } = false;

    [Description("Check for and install plugin updates during 'Update All'")]
    public bool update_plugins { get; set; } = false;

    public string temp_directory { get; set; } = null;

    public string plugins_directory { get; set; } = null;

    // Name of the Terminal.Gui theme used by the --tui interface (e.g. "Dark", "8-Bit"). No
    // [Description] - only bool settings get that (the settings menu casts described props to bool).
    public string tui_theme { get; set; } = "Dark";

    [Description("Use new pupdate UI (Beta). takes effect next launch")]
    public bool use_tui { get; set; } = false;

    // Set once the startup "which interface?" prompt has been answered, so we only ask once.
    // Internal gate (no [Description] so it isn't a menu toggle; hidden from JSON until true).
    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool ui_prompt_completed { get; set; } = false;

    [Description("Adds a description element to the video.json display modes (non-breaking)")]
    public bool add_display_mode_description_to_video_json { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    //[Description("Use a local cores.json file for the inventory")]
    public bool use_local_cores_inventory { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    //[Description("Use a local blacklist.json file")]
    public bool use_local_blacklist { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    //[Description("Use a local image_packs.json file")]
    public bool use_local_image_packs { get; set; } = false;

    //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    [Description("Use a local pocket_extras.json file")]
    public bool use_local_pocket_extras { get; set; } = false;

    [Description("Use a local pocket_library_images.json file")]
    public bool use_local_pocket_library_images { get; set; } = false;

    //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    [Description("Use a local display_modes.json file")]
    public bool use_local_display_modes { get; set; } = false;

    [Description("Use a local plugins.json file")]
    public bool use_local_plugins_catalog { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    //[Description("Use a local ignore_instance.json file")]
    public bool use_local_ignore_instance_json { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    //[Description("Suppress the 'Already Installed' messages for core files and assets")]
    public bool suppress_already_installed { get; set; }

    [Description("Hide and uninstall Analogizer core variants")]
    public bool no_analogizer_variants { get; set; } = false;

    [Description("Hide and uninstall AI-generated cores above the score threshold (Experimental)")]
    public bool filter_ai_cores { get; set; } = false;

    public int ai_core_threshold { get; set; } = 100;

    [Description("Download files in concurrent chunks (faster on bandwidth-limited servers)")]
    public bool concurrent_downloads { get; set; } = true;

    public int download_chunk_count { get; set; } = 4;

    // Plugin ids (see RetiredSettingMigrations) whose replacement install hasn't been offered yet.
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public List<string> pending_plugin_migrations { get; set; }

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<Archive> archives { get; set; } = new()
    {
        new Archive
        {
            name = "default",
            type = ArchiveType.internet_archive,
            archive_name = "openFPGA-Files"
        },
        new Archive
        {
            name = "custom",
            type = ArchiveType.custom_archive,
            archive_name = "custom",
            url = "https://updater.retrodriven.com",
            index = "updater.php"
        },
        new Archive
        {
            name = "agg23.GameAndWatch",
            type = ArchiveType.core_specific_archive,
            archive_name = "fpga-gnw-opt",
            archive_folder = null,
            file_extensions = new List<string> { ".gnw" },
            enabled = false
        }
    };

    #region Old Settings

    private string _archive_name;

    [JsonProperty]
    private string archive_name { set { _archive_name = value; } }

    private string _gnw_archive_name;

    [JsonProperty]
    private string gnw_archive_name { set { _gnw_archive_name = value; } }

    private bool? _download_gnw_roms;

    [JsonProperty]
    private bool? download_gnw_roms { set { _download_gnw_roms = value; } }

    private CustomArchive _custom_archive;

    [JsonProperty]
    private CustomArchive custom_archive { set { _custom_archive = value; } }

    // Settings retired when their feature moved to a plugin. Write-only so they drop out of the
    // file on the next save; Migrate() queues the replacement plugin offer when they were on.
    private bool? _fix_jt_names;

    [JsonProperty]
    private bool? fix_jt_names { set { _fix_jt_names = value; } }

    private bool? _coin_op_beta;

    [JsonProperty]
    private bool? coin_op_beta { set { _coin_op_beta = value; } }

    private bool? _jt_beta_github_fetch;

    [JsonProperty]
    private bool? jt_beta_github_fetch { set { _jt_beta_github_fetch = value; } }

    private bool? _jt_beta_patreon_fetch;

    [JsonProperty]
    private bool? jt_beta_patreon_fetch { set { _jt_beta_patreon_fetch = value; } }

    private string _patreon_session_cookie;

    [JsonProperty]
    private string patreon_session_cookie { set { _patreon_session_cookie = value; } }

    public void Migrate()
    {
        Archive archive;

        if (_coin_op_beta == true)
        {
            this.AddPendingPluginMigration(RetiredSettingMigrations.COINOP_LICENSE);
        }

        // default was true, so only an explicit true (an existing file) counts, not a fresh install
        if (_fix_jt_names == true)
        {
            this.AddPendingPluginMigration(RetiredSettingMigrations.JOTEGO_PLATFORM_NAMES);
        }

        if (_jt_beta_github_fetch == true || _jt_beta_patreon_fetch == true ||
            !string.IsNullOrWhiteSpace(_patreon_session_cookie))
        {
            this.AddPendingPluginMigration(RetiredSettingMigrations.JOTEGO_BETA_KEY);
        }

        if (!string.IsNullOrEmpty(_archive_name))
        {
            archive = this.archives.FirstOrDefault(x => x.name == "default");

            if (archive != null)
            {
                archive.archive_name = _archive_name;
            }
        }

        if (!string.IsNullOrEmpty(_gnw_archive_name))
        {
            archive = this.archives.FirstOrDefault(x => x.name == "agg23.GameAndWatch");

            if (archive != null)
            {
                archive.archive_name = _gnw_archive_name;
            }
        }

        if (_download_gnw_roms.HasValue)
        {
            archive = this.archives.FirstOrDefault(x => x.name == "agg23.GameAndWatch");

            if (archive != null)
            {
                archive.enabled = _download_gnw_roms.Value;
            }
        }

        archive = this.archives.FirstOrDefault(x => x.name == "custom");

        if (_custom_archive != null)
        {
            if (archive != null)
            {
                archive.url = _custom_archive.url;
                archive.index = _custom_archive.index;
            }
        }

        // bugfix: check to make sure the custom archives has archive_name populated
        if (archive is { type: ArchiveType.custom_archive } && string.IsNullOrEmpty(archive.archive_name))
        {
            archive.archive_name = "custom";
        }
    }

    private void AddPendingPluginMigration(string pluginId)
    {
        this.pending_plugin_migrations ??= new List<string>();

        if (!this.pending_plugin_migrations.Contains(pluginId, StringComparer.OrdinalIgnoreCase))
        {
            this.pending_plugin_migrations.Add(pluginId);
        }
    }

    public void ClearPendingPluginMigration(string pluginId)
    {
        this.pending_plugin_migrations?.RemoveAll(id => string.Equals(id, pluginId, StringComparison.OrdinalIgnoreCase));

        if (this.pending_plugin_migrations is { Count: 0 })
        {
            this.pending_plugin_migrations = null;
        }
    }

    #endregion
}
