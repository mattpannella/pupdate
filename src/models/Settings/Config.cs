namespace Pannella.Models.Settings;

public class Config
{
    public bool download_assets { get; set; } = true;
    public string archive_name { get; set; } = "openFPGA-Files";
    public string github_token { get; set; } = string.Empty;
    public bool download_firmware { get; set; } = true;
    public bool core_selector { get; set; } = true;
    public bool preserve_platforms_folder { get; set; } = false;
    public bool delete_skipped_cores { get; set; } = true;
    public string download_new_cores { get; set; }
    public bool build_instance_jsons { get; set; } = true;
    public bool crc_check { get; set; } = true;
    public bool fix_jt_names { get; set; } = true;
    public bool skip_alternative_assets { get; set; } = true;
    public bool backup_saves { get; set; }
    public string backup_saves_location { get; set; } = "Backups";
    public bool use_custom_archive { get; set; } = false;

    public Dictionary<string, string> custom_archive { get; set; } = new()
    {
        { "url", "https://updater.retrodriven.com" },
        { "index", "updater.php" }
    };
}
