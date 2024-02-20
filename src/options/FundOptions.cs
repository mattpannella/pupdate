using CommandLine;

namespace Pannella.Options;

[Verb("fund", HelpText = "List sponsor links")]
public class FundOptions : BaseOptions
{
    [Option('c', "core", HelpText = "The core to check funding links for", Required = false)]
    public string Core { get; set; }
}
