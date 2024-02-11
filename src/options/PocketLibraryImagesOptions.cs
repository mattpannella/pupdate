using CommandLine;

namespace Pannella.Options
{
    [Verb("pocket-library-images", HelpText = "Downloads and installs the Pocket Library Images")]
    public class PocketLibraryImagesOptions
    {
        [Option('p', "path", HelpText = "Absolute path to install location", Required = false)]
        public string InstallPath { get; set; }
    }
}
