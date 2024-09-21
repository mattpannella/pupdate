using System.ComponentModel;
using Newtonsoft.Json;
// ReSharper disable RedundantDefaultMemberInitializer
// ReSharper disable InconsistentNaming

namespace Pannella.Models.Settings;

public class Config
{
    [Description("Download Missing Assets (ROMs and BIOS Files) during 'Update All'")]
    public bool download_assets { get; set; } = true;

    public string github_token { get; set; } = string.Empty;

    [Description("Download Firmware Updates during 'Update All'")]
    public bool download_firmware { get; set; } = true;

    [Description("Preserve 'Platforms' folder during 'Update All'")]
    public bool preserve_platforms_folder { get; set; } = false;

    [Description("Delete untracked cores during 'Update All'")]
    public bool delete_skipped_cores { get; set; } = false;

    public string download_new_cores { get; set; }

    [Description("Build game JSON files for supported cores during 'Update All'")]
    public bool build_instance_jsons { get; set; } = true;

    [Description("Use CRC check when checking ROMs and BIOS files")]
    public bool crc_check { get; set; } = true;

    [Description("Automatically rename Jotego cores during 'Update All'")]
    public bool fix_jt_names { get; set; } = true;

    [Description("Skip alternative roms when downloading assets")]
    public bool skip_alternative_assets { get; set; } = true;

    [Description("Compress and backup Saves and Memories directories during 'Update All'")]
    public bool backup_saves { get; set; }

    public string backup_saves_location { get; set; } = "Backups";

    [Description("Show descriptions for advanced menu items")]
    public bool show_menu_descriptions { get; set; } = true;

    [Description("Use custom asset archive")]
    public bool use_custom_archive { get; set; } = false;

    [Description("Automatically install updates to Pupdate")]
    public bool auto_install_updates { get; set; } = false;

    [Description("Coin-Op Collection Beta Access")]
    public bool coin_op_beta { get; set; } = false;

    public string temp_directory { get; set; } = null;

    public string patreon_email_address { get; set; } = null;

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

    //[JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    [Description("Use a local display_modes.json file")]
    public bool use_local_display_modes { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    //[Description("Use a local ignore_instance.json file")]
    public bool use_local_ignore_instance_json { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    //[Description("Suppress the 'Already Installed' messages for core files and assets")]
    public bool suppress_already_installed { get; set; }

    [JsonProperty(ObjectCreationHandling = ObjectCreationHandling.Replace)]
    public List<Archive> archives { get; set; } = new()
    {
        new Archive
        {
            name = "default",
            type = ArchiveType.internet_archive,
            archive_name = "openFPGA-Files",
        },
        new Archive
        {
            name = "custom",
            type = ArchiveType.custom_archive,
            archive_name = "custom",
            url = "https://updater.retrodriven.com",
            index = "updater.php",
        },
        new Archive
        {
            name = "agg23.GameAndWatch",
            type = ArchiveType.core_specific_archive,
            archive_name = "fpga-gnw-opt",
            archive_folder = null,
            file_extensions = new List<string> { ".gnw" },
            enabled = false,
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

    public void Migrate()
    {
        Archive archive;

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

    #endregion
}
