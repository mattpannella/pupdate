using CommandLine;

namespace Pannella.Options;

public class BaseOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }
}
