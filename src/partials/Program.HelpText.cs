namespace Pannella;

internal partial class Program
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

  gameboy-palettes         Run the instance JSON generator
    -p, --path                Absolute path to install location

  pocket-library-images    Run the instance JSON generator
    -p, --path                Absolute path to install location

  pocket-extras            Download Pocket Extras
    -p, --path                Absolute path to install location
    -n, --name                The name of the extra to install. Required
    -i, --info                Shows the details for the specified 'name'
    -l, --list                Lists out all of the values for 'name' and their details

  display-modes            Enable all Display Modes
    -p, --path                Absolute path to install location

  update-self              Check for updates to pupdate

  help                     Display more information on a specific command.

  version                  Display version information.
""";
}
