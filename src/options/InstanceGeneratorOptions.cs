using CommandLine;

namespace Pannella.Options;

[Verb("instance-generator", aliases: new[] { "instancegenerator" }, HelpText = "Run the instance JSON generator for PC Engine CD" )]
public class InstanceGeneratorOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }
}
