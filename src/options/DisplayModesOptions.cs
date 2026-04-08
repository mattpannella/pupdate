using CommandLine;

namespace Pannella.Options;

[Verb("display-modes",  HelpText = "Apply recommended (curated) display modes from display_modes.json")]
public class DisplayModesOptions : BaseOptions { }
