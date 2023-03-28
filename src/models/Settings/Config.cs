namespace pannella.analoguepocket;

public class Config
{
    public bool download_assets { get; set; }
    public string archive_name { get; set; }
    public string? github_token { get; set; }
    public bool download_firmware { get; set; }
    public bool core_selector { get; set; }
    public bool preserve_platforms_folder { get; set; }
    public bool delete_skipped_cores { get; set; }
    public string? download_new_cores { get; set; }
    public bool build_instance_jsons { get; set; }
    public bool crc_check { get; set; }
    public bool fix_jt_names { get; set; }
    public bool skip_alternative_assets { get; set; }
    public bool use_custom_archive { get; set; }
    public Dictionary<string, string> custom_archive { get; set; }

    public Config()
    {
        download_assets = true;
        download_firmware = true;
        archive_name = "openFPGA-Files";
        core_selector = true;
        preserve_platforms_folder = false;
        delete_skipped_cores = true;
        download_new_cores = null;
        build_instance_jsons = true;
        crc_check = true;
        fix_jt_names = true;
        skip_alternative_assets = true;
        use_custom_archive = false;
        custom_archive = new Dictionary<string, string>() {
            {"url", "https://updater.retrodriven.com"},
            {"index", "updater.php"}
        };
    }
}