
[![Current Release](https://img.shields.io/github/v/release/mattpannella/pupdate?label=Current%20Release)](https://github.com/mattpannella/pupdate/releases/latest) ![Downloads](https://img.shields.io/github/downloads/mattpannella/pupdate/latest/total?label=Downloads) [![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate/?business=YEERX89E75HQ8&no_recurring=1&currency_code=USD)

A free utility for updating the openFPGA cores, firmware, and a bunch of other stuff on your Analogue Pocket.

A complete list of available cores can also be found here: [https://openfpga-cores-inventory.github.io/analogue-pocket/](https://openfpga-cores-inventory.github.io/analogue-pocket/)

I can't (and don't want to) support old versions, so please make sure you download the latest release before submitting any issues.

## Easy Mode ##

If you just want to use this utility, do not clone the source repository. Just download the [latest release](https://github.com/mattpannella/pupdate/releases/latest/). Unzip it, put the executable file for your platform (windows, mac os, or linux) in the root of your sd card, and run the program.

At the main menu run `Settings` to have it walk through the available settings for you.

## Interactive Console Menu ##

For a full view of the interactive console menu, see [here](MENU.md).

## RTFM ##

[Update All](#update-all) |
[Update Firmware](#update-firmware) |
[Select Cores](#select-cores) |
[Download Assets](#download-assets) |
[Backup Saves & Memories](#backup-saves--memories) |
[Image Packs](#pocket-setup---download-platform-image-packs) |
[Library Images](#pocket-setup---download-pocket-library-images) |
[GameBoy Palettes](#pocket-setup---download-gameboy-palettes) |
[PC Engine CD](#pocket-setup---generating-instance-json-files-pc-engine-cd) |
[Game & Watch](#pocket-setup---generate-game--watch-roms) |
[Display Modes](#pocket-setup---enable-all-display-modes) |
[Super GameBoy Aspect Ratio](#pocket-setup---super-gameboy-aspect-ratio) |
[Pocket Maintenance](#pocket-maintenance---reinstall-or-uninstall-cores) |
[Pocket Extras](#pocket-extras) |
[Settings](#settings) |
[Additional Settings](#additional-settings) |
[CLI Commands and Parameters](#cli-commands-and-parameters) |
[Jotego Beta Cores](#jotego-beta-cores) |
[Jotego Cores Analogizer Setup](#jotego-cores-analogizer-setup) |
[Coin-Op Collection Beta Cores](#coin-op-collection-beta-cores) |

### Update All
Install/Update all of your cores, plus a bunch of other stuff. It can basically be used as the "do everything I want" option. Everything marked with a * can be turned on/off via settings.
1. Checks for new firmware updates *
2. Compress and backup Saves and Memories *
3. Installs/updates every core you have selected
4. Checks for missing required assets for each core you have selected *
5. Deletes cores that you do not have selected *
6. Runs the instance JSON generator for each core you have selected (currently, only PC Engine CD) *
7. Rename every Jotego core you have selected *

### Update Selected
Presents you with a list of your installed cores and lets you choose which ones you want to update.

### Install Selected
Presents you with a list of cores you don't have installed and lets you choose which ones you want to install and then immediately installs them with having to run Update All.

### Update Firmware
Self-explanatory. Just checks for firmware updates and exits.

### Select Cores

This will prompt you to ask if you want new cores installed by default, with 3 options:
- Yes
    - Selecting this automatically chooses all existing cores, and will continue to automatically install new cores as they are released.
- No
    - Selecting this means as new cores are released, they will not be installed automatically, nor will you be asked about them. Then you will be presented with a list of all currently available cores, to select from for yourself.
- Ask
    - Selecting this means as new cores are released, you will be notified each time you run the app and have the option to select them for installation.  Then you will be presented with a list of all currently available cores, to select from for yourself.

### Download Assets

Checks for missing assets for each core you have selected (mainly arcade ROM files and BIOSes).

_Note: You are responsible for finding and adding your own ROMs for non-arcade cores._

### Backup Saves & Memories

This will compress the Saves and Memories directories from your Pocket to the location specified in the config settings.

### Pocket Setup - Display Modes

- Enable Recommended Display Modes
  
This enables a curates set of display modes and applies them to specific cores that you have installed. This list can be found in the [`display_modes.json`](display_modes.json) file. If you wish to make changes to this file, download it from GitHub and place it in the same directory as the pupdate executable. Then set `use_local_display_modes` to `true` in your `pupdate_settings.json` file.

- Enable Selected Display Modes for All Cores

This presents you with a list of all of the supported display modes and lets you select which ones you want to apply. Then it applies those display modes to all of the cores you have installed.

- Enable Selected Display Modes for Select Cores

This presents you with a list of all of the supported display modes and lets you select which ones you want to apply. Next, you'll be asked to select which of your installed cores you want to apply the display modes to. Then it applies those display modes to the cores you have selected.

### Pocket Setup - Download Platform Image Packs

This will present you with a list of available image packs and automatically download and extract it to the Platforms/_images directory for you

### Pocket Setup - Download Pocket Library Images

This will download the System Library Images that were published by Spiritualized1997. They are used for the Library functionality on the Pocket.

### Pocket Setup - Download GameBoy Palettes

This will download the palette files for the Pocket GameBoy cartridges. This is currently maintained by [davewongillies](https://github.com/davewongillies/) on [GitHub](https://github.com/davewongillies/openfpga-palettes) and by R.A.Helllord on [Discord](https://discord.com/channels/834264850230018058/1199625145405407273) and [Reddit](https://www.reddit.com/r/AnaloguePocket/comments/18q2iz1/collection_of_all_official_game_boy_color_and/).

Contents:
- All official GBC, SGB, NSO, and 3DS VC palettes
- Custom palettes for all Limited Edition Pockets
- SGB2 Vaporwave Edition palettes courtesy of flamepanther
- Trashuncle's palettes for Mister
- Sameboy and BGB palettes
- Pipboy palettes (Amber, Green, Blue, and White)
- 300+ palettes covering a ton of systems and themes by TheWolfBunny64

### Pocket Setup - Generating Instance JSON Files (PC Engine CD)

- Only supported by PC Engine CD, currently

- Put your games in /Assets/{platform}/common

- Each game needs to be in its own directory (and be sure to name the directory the full title of the game)

- Examples:
    - /Assets/pcecd/common/Rondo of Blood
    - /Assets/pcecd/common/Bonk
    - etc

- All games (for PC Engine CD) must be in cue/bin format. The generated json file will be saved using the same filename as the cue file, so be sure to also name that with the full title of the game

- When you run the `Generate Instance JSON Files` or `Update All` menu items, it will search through every directory in common and create a json file that can be launched by the core

- You can disable this process in Update All by setting `build_instance_jsons` to `false` in your settings file, if you don't want it to run every time you update.

### Pocket Setup - Generate Game & Watch ROMs

How to build game and watch roms that are compatible with the Pocket:

Create 2 new folders.

`/Assets/gameandwatch/agg23.GameAndWatch/artwork` and `/Assets/gameandwatch/agg23.GameAndWatch/roms`

Place your `[artwork].zip` files into the artwork folder and your `[rom]`.zip files into the roms folder

Should look like this:

```
/Assets/gameandwatch/agg23.GameAndWatch/artwork/gnw_dkong.zip

/Assets/gameandwatch/agg23.GameAndWatch/roms/gnw_dkong.zip
```

Now just run the menu option in the updater and it will build your games

### Pocket Setup - Enable All Display Modes

This will enable all of the Pocket Display Modes for the openFPGA cores.

### Pocket Setup - Super GameBoy Aspect Ratio

This allows you to apply the 8:7 aspect ratio to any of the Super GameBoy cores you may have installed. When selected you will be asked which cores you want to do this for.

The 8:7 aspect ratio gives you a more 'full screen' look and feel on the Pocket.

You also have the ability to reset any of the Super GameBoy cores back to the original 4:3 aspect ratio.

### Pocket Maintenance - Reinstall or Uninstall Cores

These give you the ability to reinstall all or select cores. The reinstall will erase and reinstall all core specific files and assets. It will not touch your ROMs or Save files.

If you wish to uninstall one or more cores, selecting 'Uninstall Select Cores' will allow you to choose which cores you'd like to remove. It will also prompt you to ask if you wish to remove core specific assets. Doing so will not touch your ROMs or Save files. 

### Pocket Extras

This section contains some extra functionality that individuals have created. Each item in this menu will provide a description and links to the authors GitHub before installing it. That way you get more information and can choose accordingly.

_Note: If you are an advanced user, you can disable the description functionality by setting the `show_menu_description` config setting to `false`_

There are 3 main categories:

#### Additional Assets

This contains extras that enhance or modify existing cores. For example, addition additional ROM support to Arcade cores or allowing an Arcade core to load multiple games rather than just one.

This type of extra requires you have the necessary core installed. If you don't, you will be prompted to install it.

#### Combination Platforms

This contains new platforms that combine multiple cores into a single one. This helps in reducing the number of items you see in your openFPGA menu on your Pocket and to leverage the 'Change Core' functionality.

#### Variant Cores

This contains a list of alternate core setups. These take existing cores and make copies of them with some changes, leaving the original core intact and providing new functionality with the additional core.

### Settings

 The following settings are all available via the `Settings` menu item.

| Name                                     | Description                                                                                                                                                                                                                                                                      |
|------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Download Firmware Updates                | Check for firmware updates when running "Update All"                                                                                                                                                                                                                             |
| Download Missing Assets                  | Check for missing assets (ROMs, BIOS files, etc) when running "Update All"                                                                                                                                                                                                       |
| Download Game & Watch ROMS               | Download Game & Watch ROMS when running "Update All"                                                                                                                                                                                                                             |
| Build game JSON Files                    | Run the Instance JSON builder during "Update All"                                                                                                                                                                                                                                |
| Delete untracked cores                   | Any core that is available, but you have not chosen in the "Core Selector" will be uninstalled, if found on your SD card when running "Update All"                                                                                                                               |
| Automatically rename Jotego cores        | Jotego's cores will be renamed to the correct titles of the platforms they are emulating, when running "Update All". example: jtcontra is Contra                                                                                                                                 |
| Use CRC check                            | Use CRC file hashes to verify Asset files, and re-download if needed. When running "Update All" or "Download Required Assets"                                                                                                                                                    |
| Preserve Platforms folder                | Don't overwrite changes made to files in the Platforms folder when running "Update All"                                                                                                                                                                                          |
| Skip alternative ROMs                    | Ignore files if they are in a folder named "_alternatives" when checking for Assets (Note: this is on by default)                                                                                                                                                                |
| Compress and backup Saves and Memories   | This will compress and backup the Saves and Memories directory to the specified location. By default, a Backups directory will be created off the root. The location can be changed manually by setting the "backup_saves_location" with the absolute path in the settings file. |
| Show Menu Descriptions                   | This will show descriptions for some of the advanced menu items after they are selected, including a prompt asking if you want to proceed. This is enabled by default.                                                                                                           |
| Use custom archive                       | Allows you to use a custom site for Asset file checking (there is a pre-configured site available). The actual URL of the custom site can be set manually by editing the settings file in an editor.                                                                             |
| Automatically install updates to Pupdate | Turn this on and the application will automatically update itself without asking for input, when you start it.                                                                                                                                                                   |
| Use Local Pocket Extras                  | Turn this on and place the `pocket_extras.json` file in the same directory as `pupdate` and it will use the local file instead of getting it from GitHub.                                                                                                                        |
| Use Local Display Modes                  | Turn this on and place the `display_modes.json` file in the same directory as `pupdate` and it will use the local file instead of getting it from GitHub.                                                                                                                        |


### Additional Settings

 The following settings can be set by editing `pupdate_settings.json` in a text editor.

| Name                         | Description                                                                                                                                                                                                                                                                                                                                                                                                     |
|------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| config.archive_name          | The account on archive.org that the app will use to check for Assets                                                                                                                                                                                                                                                                                                                                            |
| config.github_token          | The app will use this when making API calls to GitHub                                                                                                                                                                                                                                                                                                                                                           |
| config.download_new_cores    | This will be set automatically by the `Select Cores` menu item. It can be set to "yes", "no", or "ask"                                                                                                                                                                                                                                                                                                          |
| config.custom_archive        | You can set a custom URL here, if you don't want to use the default. `index` is a relative path to the index of your custom site's files. This is not required, but it's needed for CRC checking. If you have CRC checking enabled, the setting will be ignored unless this provides the necessary format. It must match the output of archive.org's json endpoint. https://archive.org/developers/md-read.html |
| config.backup_saves          | Set this to `true` if you want your Saves directory to be backed up automatically during `Update All`                                                                                                                                                                                                                                                                                                           |
| config.backup_saves_location | Put the absolute path to the backup location here to backup your `Saves` directory to. This defaults to `Backups` in the current directory when not set.                                                                                                                                                                                                                                                        |
| config.temp_directory        | When left `null` all zip files are downloaded and extracted using a temp directory in your pocket install location. If you supply a path, that will be used, instead                                                                                                                                                                                                                                            |
| core_settings                | This allows you to set a subset of settings on a per core basis. It contains a list of every core, with 3 options. `skip`, `download_assets`, and `platform_rename`. You can use these to override your global defaults                                                                                                                                                                                         |

## CLI Commands and Parameters

```
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

  display-modes            Enable the curated list of Display Modes for all cores
    -p, --path                Absolute path to install location

  prune-memories           Delete all but the latest save states for each game (defaults to all cores)
   -p, --path                 Absolute path to install location
   -c, --core                 The core you want to prune memories for

  update-self              Check for updates to pupdate

  help                     Display more information on a specific command.

  version                  Display version information.
```

examples:

`/path/to/pupdate -p /path/to/sdcard/`

`/path/to/pupdate update -c boogermann.bankpanic`

`/path/to/pupdate assets -c jotego.jtcontra`

`/path/to/pupdate images -i pocket-platform-images -o dyreschlock -v home`

## Jotego Beta Cores

Now that Jotego is releasing his beta cores publicly (and requiring a beta key to play them), you can just drop the `jtbeta.zip` file from patreon onto the root of your sd card and run Update All, and it will automatically copy the beta key to the correct folders for the cores that need it. It also will let you install the yhe cores directly from the updater, now. Make sure you don't rename the file, it's going to look for exactly `jtbeta.zip`

## Jotego Cores Analogizer Setup

To set your global configuration for the Analogizer in Jotego's Cores just go to `Pocket Setup` > `Jotego Analogizer Config` and it will walk you through the available settings. The settings will be saved to the correct location, so you don't need to do anything else. For more help please refer to his github repo.

## Coin-Op Collection Beta Cores

- Go into Settings and turn on the Coin-Op Beta setting
- Next time you run Update All it will prompt you to enter your email address that you use for patreon.
- From now on each time you run Update All it will pull your beta license automatically, so that when you install Coin-Op beta cores, they will function correctly
- If you ever want to change the email address, go into the Pocket Setup menu

## Troubleshooting

 - Slow asset downloads? Try toggling `use_custom_archive` to true, in your settings.

 - If you run the update process and get a message like `Error in framework RS: bridge not responding` when running a core, try to run the updater in a local folder on your pc, and then copy the files over to the sd card afterwards. I'm not entirely sure what the issue is, but I've seen it reported a bunch of times now and running the updater locally seems to help.

 - I keep getting a `Missing ROM ID [1]` message when trying to launch arcade games in the _alternatives folder. Check your settings and make sure you have `Skip Alternative ROMS` turned off.
  

## Submitting new cores ##

You can submit new cores here [https://github.com/openfpga-cores-inventory/analogue-pocket](https://github.com/openfpga-cores-inventory/analogue-pocket)

## Credits ##

Thanks to [neil-morrison44](https://github.com/neil-morrison44). This is a port built on top of the work originally done by him [here](https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8).

### With special thanks to: ### 
[Michael Hallett](https://github.com/hallem) for contributing a ton to the project.

[RetroDriven](https://github.com/RetroDriven/) for maintaining the arcade rom archive.

[dyreschlock](https://github.com/dyreschlock/pocket-platform-images/tree/main/arcade/Platforms) for hosting the updated platform files for Jotego's cores.

[espiox](https://github.com/espiox) for maintaining the game & watch rom archive.

## Other Options ##

If you're looking for something with a few more features and a user interface, check out this updater. [https://github.com/RetroDriven/Pocket_Updater](https://github.com/RetroDriven/Pocket_Updater)

Or if you want something cross platform that will run on a mac or linux: [https://github.com/neil-morrison44/pocket-sync](https://github.com/neil-morrison44/pocket-sync)
