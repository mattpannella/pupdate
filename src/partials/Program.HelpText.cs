namespace Pannella;

internal static partial class Program
{
    private const string HELP_TEXT =
"""
Usage:

  menu                     Interactive Main Menu (Default Verb)
    -p, --path                Absolute path to install location
    -s, --skip-update         Go straight to the menu, without looking for an update

  fund                     List sponsor links. Lists all if no core is provided
    -p, --path                Absolute path to install location
    -c, --core                The core to check funding links for
    
  update                   Run update all. (Can be configured via the settings menu)
    -p, --path                Absolute path to install location
    -c, --core                The core you want to update. Runs for all otherwise
    -r, --clean               Clean install. Remove all existing core files, and force a fresh re-install
  
  uninstall                Delete a core
    -p, --path                Absolute path to install location
    -c, --core                The core you want to uninstall. Required
    -a, --assets              Delete the core specific Assets folder. ex: Assets/{platform}/{corename}

  assets                   Run the asset downloader
    -p, --path                Absolute path to install location
    -c, --core                The core you want to download assets for.

  firmware                 Check for Pocket firmware updates
    -p, --path                Absolute path to install location

  images                   Download image packs
    -p, --path                Absolute path to install location
    -o, --owner               Image pack repo username
    -i, --imagepack           Github repo name for image pack
    -v, --variant             The optional variant

  instance-generator       Run the instance JSON generator for PC Engine CD
    -p, --path                Absolute path to install location

  backup-saves             Compress and backup Saves & Memories directories
    -p, --path                Absolute path to install location
    -l, --location            Absolute path to backup location. Required
    -s, --save                Save settings to the config file for use during 'Update All'

  gameboy-palettes         Download and install Game Boy color palettes
    -p, --path                Absolute path to install location

  pocket-library-images    Download and install Pocket library images
    -p, --path                Absolute path to install location

  pocket-extras            Download Pocket Extras
    -p, --path                Absolute path to install location
    -n, --name                The extra to install or query (required unless -l)
    -i, --info                Show details for the specified name
    -l, --list                List all extras and their details

  display-modes            Apply recommended (curated) display modes from display_modes.json
    -p, --path                Absolute path to install location

  analogizer-setup         Set up Analogizer options
    -p, --path                Absolute path to install location
    -j, --jotego              Run setup for Jotego cores

  prune-memories           Prune old save states (memories) for installed cores
    -p, --path                Absolute path to install location
    -c, --core                Limit pruning to this core (optional)

  clear-archive-cache      Delete cached archive downloads (requires cache archive files enabled in settings)
    -p, --path                Absolute path to install location
    -y, --yes                 Confirm clearing (required)

  update-self              Check for updates to pupdate

  help                     Display more information on a specific command.

  version                  Display version information.
""";
}
