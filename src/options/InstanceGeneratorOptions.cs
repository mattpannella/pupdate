using CommandLine;

namespace Pannella.Options;

[Verb("instancegenerator",  HelpText = "Run the instance JSON generator")]
public class InstanceGeneratorOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }
}
