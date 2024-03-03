using CommandLine;

namespace Pannella.Options;

[Verb("images",  HelpText = "Download image packs")]
public class ImagesOptions : BaseOptions
{
    [Option('o', "owner", Required = true, HelpText = "Image pack repo username")]
    public string ImagePackOwner { get; set; }

    [Option('i', "imagepack", Required = true, HelpText = "Github repo name for image pack")]
    public string ImagePackRepo { get; set; }

    [Option('v', "variant", Required = false, HelpText = "The optional variant")]
    public string ImagePackVariant { get; set; }
}
