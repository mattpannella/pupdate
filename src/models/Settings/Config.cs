using Newtonsoft.Json;

namespace Pannella.Models.Settings;

public class Config
{
    public bool download_assets { get; set; } = true;
    public string github_token { get; set; } = string.Empty;
    public bool download_firmware { get; set; } = true;
    public bool preserve_platforms_folder { get; set; } = false;
    public bool delete_skipped_cores { get; set; } = true;
    public string download_new_cores { get; set; }
    public bool build_instance_jsons { get; set; } = true;
    public bool crc_check { get; set; } = true;
    public bool fix_jt_names { get; set; } = true;
    public bool skip_alternative_assets { get; set; } = true;
    public bool backup_saves { get; set; }
    public string backup_saves_location { get; set; } = "Backups";
    public bool show_menu_descriptions { get; set; } = true;
    public bool use_custom_archive { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool use_local_blacklist { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool use_local_image_packs { get; set; } = false;

    [JsonProperty(DefaultValueHandling = DefaultValueHandling.Ignore)]
    public bool use_local_pocket_extras { get; set; } = false;

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
        if (!string.IsNullOrEmpty(_archive_name))
        {
            var archive = this.archives.FirstOrDefault(x => x.name == "default");

            if (archive != null)
            {
                archive.archive_name = _archive_name;
            }
        }

        if (!string.IsNullOrEmpty(_gnw_archive_name))
        {
            var archive = this.archives.FirstOrDefault(x => x.name == "agg23.GameAndWatch");

            if (archive != null)
            {
                archive.archive_name = _gnw_archive_name;
            }
        }

        if (_download_gnw_roms.HasValue)
        {
            var archive = this.archives.FirstOrDefault(x => x.name == "agg23.GameAndWatch");

            if (archive != null)
            {
                archive.enabled = _download_gnw_roms.Value;
            }
        }

        if (_custom_archive != null)
        {
            var archive = this.archives.FirstOrDefault(x => x.name == "custom");

            if (archive != null)
            {
                archive.url = _custom_archive.url;
                archive.index = _custom_archive.index;
            }
        }
    }

    #endregion
}
