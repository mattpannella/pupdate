using CommandLine;

namespace Pannella.Options;

[Verb("validate-cores", HelpText = "Check installed cores for missing or invalid JSON files")]
public class ValidateCoresOptions : BaseOptions
{
    [Option('f', "fix", Required = false, HelpText = "Reinstall (clean) any cores found with missing or invalid JSON.")]
    public bool Fix { get; set; }
}
