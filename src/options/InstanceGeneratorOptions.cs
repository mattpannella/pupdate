using CommandLine;

namespace Pannella.Options;

[Verb("instance-generator", aliases: new[] { "instancegenerator" }, HelpText = "Run the instance JSON generator for PC Engine CD" )]
public class InstanceGeneratorOptions : BaseOptions { }
