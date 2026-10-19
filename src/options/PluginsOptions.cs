using CommandLine;

namespace Pannella.Options;

[Verb("plugins", HelpText = "List, install, run, update or remove plugins")]
public class PluginsOptions : BaseOptions
{
    [Option('l', "list", Required = false, HelpText = "List installed plugins")]
    public bool List { get; set; }

    [Option('b', "browse", Required = false, HelpText = "List the plugins in the catalog")]
    public bool Browse { get; set; }

    [Option('i', "install", Required = false, HelpText = "Install a plugin: owner/repo, a github.com URL, or a catalog id")]
    public string Install { get; set; }

    [Option('r', "run", Required = false, HelpText = "Run a plugin by name (or directory name)")]
    public string Run { get; set; }

    [Option("remove", Required = false, HelpText = "Uninstall a plugin by name (or directory name)")]
    public string Remove { get; set; }

    [Option('u', "update", Required = false, HelpText = "Update installed plugins (all, or just -n)")]
    public bool Update { get; set; }

    [Option('n', "name", Required = false, HelpText = "Limit --update to this plugin")]
    public string Name { get; set; }

    [Option("set-auto", Required = false, HelpText = "Run this plugin during 'Update All' (see --phase)")]
    public string SetAuto { get; set; }

    [Option("phase", Required = false, Default = "after", HelpText = "before or after cores, for --set-auto")]
    public string Phase { get; set; }

    [Option("unset-auto", Required = false, HelpText = "Stop running this plugin during 'Update All'")]
    public string UnsetAuto { get; set; }
}
