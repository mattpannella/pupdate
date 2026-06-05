using CommandLine;

namespace Pannella.Options;

public class BaseOptions
{
    [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
    public string InstallPath { get; set; }

    [Option('y', "yes", HelpText = "Non-interactive mode: assume the default answer to all prompts and skip the self-update prompt. Intended for CI/cron/unattended runs.", Required = false)]
    public bool AssumeYes { get; set; }
}
