
[![Current Release](https://img.shields.io/github/v/release/mattpannella/pupdate?label=Current%20Release)](https://github.com/mattpannella/pupdate/releases/latest) ![Downloads](https://img.shields.io/github/downloads/mattpannella/pupdate/latest/total?label=Downloads) [![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate/?business=YEERX89E75HQ8&no_recurring=1&currency_code=USD)

  

A free utility for updating the openFPGA cores, firmware, and a bunch of other stuff on your Analogue Pocket.

A complete list of available cores can also be found here: [https://openfpga-cores-inventory.github.io/analogue-pocket/](https://openfpga-cores-inventory.github.io/analogue-pocket/)

I can't (and don't want to) support old versions, so please make sure you download the latest release before submitting any issues.

  

## Easy Mode ##

If you just want to use this utility, do not clone the source repository. Just download the [latest release](https://github.com/mattpannella/pupdate/releases/latest/). Unzip it, put the executable file for your platform (windows, mac os, or linux) in the root of your sd card, and run the program.

  

At the main menu run `Settings` to have it walk through the available settings for you.

## Important Menu Items ##

### Update All
Install/Update all of your cores, plus a bunch of other stuff. It can basically be used as the "do everything I want" option. Everything marked with a * can be turned on/off via settings.
1. Checks for new firmware updates *
2. Installs/updates every core you have selected
3. Checks for missing required assets for each core you have selected *
4. Deletes cores that you do not have selected *
5. Runs the instance JSON generator for each core you have selected (currently, only PC Engine CD) *
6. Rename every Jotego core you have selected *

### Update Firmware
Self explanatory. Just checks for firmware updates and exits.

### Download Required Assets
Checks for missing required assets for each core you have selected (mainly arcade ROM files and BIOSes)

## RTFM ##


[Settings](#settings) |
[Additional Settings](#additional-settings) |
[CLI Commands and Parameters](#cli-commands-and-parameters) |
[Image Packs](#download-image-packs) |
[Core Selector](#core-selector) |
[PC Engine CD](#generating-instance-json-files) |
[Jotego Cores](#jotego-beta-cores) |
[Game n Watch](#how-to-build-game-and-watch-roms-that-are-compatible-with-the-pocket)


### Settings

 The following settings are all available via the `Settings` menu item.

  

|                                     |                                                                                                                                                                                                                                                                     |
|-------------------------------------|---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Download Firmware Updates           | Check for firmware updates when running "Update All"                                                                                                                                                                                                                |
| Download Missing Assets             | Check for missing assets (ROMs, BIOS files, etc) when running "Update All"                                                                                                                                                                                          |
| Download Game & Watch ROMS          | Download Game & Watch ROMS when running "Update All"                                                                                                                                                                                          |
| Build game JSON Files               | Run the Instance JSON builder during "Update All"                                                                                                                                                                                                                   |
| Delete untracked cores              | Any core that is available, but you have not chosen in the "Core Selector" will be uninstalled, if found on your SD card when running "Update All"                                                                                                                  |
| Automatically rename Jotego cores   | Jotego's cores will be renamed to the correct titles of the platforms they are emulating, when running "Update All". example: jtcontra is Contra                                                                                                                    |
| Use CRC check                       | Use CRC file hashes to verify Asset files, and re-download if needed. When running "Update All" or "Download Required Assets"                                                                                                                                       |
| Preserve Platforms folder           | Don't overwrite changes made to files in the Platforms folder when running "Update All"                                                                                                                                                                             |
| Skip alternative ROMs               | Ignore files if they are in a folder named "_alternatives" when checking for Assets (Note: this is on by default)                                                                                                                                                   |
| Compress and backup Saves and Memories | This will compress and backup the Saves and Memories directory to the specified location. By default, a Backups directory will be created off the root. The location can be changed manually by setting the "backup_saves_location" with the absolute path in the settings file. | 
| Use custom archive                  | Allows you to use a custom site for Asset file checking (there is a pre-configured site available). The actual URL of the custom site can be set manually by editing the settings file in an editor.                                                                |

### Additional Settings

 The following settings can be set by editing `pupdate_settings.json` in a text editor.

|                              |                                                                                                                                                                                                                                                                                                                                                                                                                 |
|------------------------------|-----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| config.archive_name          | The account on archive.org that the app will use to check for Assets                                                                                                                                                                                                                                                                                                                                            |
| config.github_token          | The app will use this when making API calls to GitHub                                                                                                                                                                                                                                                                                                                                                           |
| config.download_new_cores    | This will be set automatically by the `Select Cores` menu item. It can be set to "yes", "no", or "ask"                                                                                                                                                                                                                                                                                                          |
| config.custom_archive        | You can set a custom URL here, if you don't want to use the default. `index` is a relative path to the index of your custom site's files. This is not required, but it's needed for CRC checking. If you have CRC checking enabled, the setting will be ignored unless this provides the necessary format. It must match the output of archive.org's json endpoint. https://archive.org/developers/md-read.html |
| config.backup_saves          | Set this to `true` if you want your Saves directory to be backed up automatically during `Update All`                                                                                                                                                                                                                                                                                                           |
| config.backup_saves_location | Put the absolute path to the backup location here to backup your `Saves` directory to. This defaults to `Backups` in the current directory when not set.                                                                                                                                                                                                                                                        |
| coreSettings                 | This allows you to set a subset of settings on a per core basis. It contains a list of every core, with 3 options. `skip`, `download_assets`, and `platform_rename`. You can use these to override your global defaults                                                                                                                                                                                         |

  

### CLI Commands and Parameters


```

  menu                     Interactive Main Menu (Default Verb)
    -p, --path                Absolute path to install location
    -s, --skip-update         Go straight to the menu, without looking for an update

  fund                     List sponsor links. Lists all if no core is provided
    -c, --core                The core to check funding links for
    
  update                   Run update all. (Can be configured via the settings menu)
    -p, --path                Absolute path to install location
    -c, --core                The core you want to update. Runs for all otherwise
    -f, --platformsfolder     Preserve the Platforms folder, so customizations aren't overwritten by updates.
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

  instancegenerator        Run the instance JSON generator
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
                                 Values: DonkeyKong, RadarScope, jtbubl, jtcps15, jtcps2
                                         jtpang, toaplan2_c, jtc16_c

  update-self          Check for updates to pupdate

  help                     Display more information on a specific command.

  version                  Display version information.

```

examples:

  

`/path/to/pupdate -p /path/to/sdcard/`

  

`/path/to/pupdate update -c boogermann.bankpanic`

  

`/path/to/pupdate assets -c jotego.jtcontra`

  

`/path/to/pupdate images -i pocket-platform-images -o dyreschlock -v home`

### Core Selector

This will prompt you to ask if you want new cores installed by default, with 3 options:
 - Yes
   - Selecting this automatically chooses all existing cores, and will continue to automatically install new cores as they are released.
 - No
   - Selecting this means as new cores are released, they will not be installed automatically, nor will you be asked about them. Then you will be presented with a list of all currently available cores, to select from for yourself.
 - Ask
   - Selecting this means as new cores are released, you will be notified each time you run the app and have the option to select them for installation.  Then you will be presented with a list of all currently available cores, to select from for yourself.


### Download Image Packs

This will present you with a list of available image packs and automatically download and extract it to the Platforms/_images directory for you

  
  

### Generating Instance JSON Files

- Only supported by PC Engine CD, currently

- Put your games in /Assets/{platform}/common

- Each game needs to be in its own directory (and be sure to name the directory the full title of the game)

- Example: /Assets/pcecd/common/Rondo of Blood

- /Assets/pcecd/common/Bonk

- etc

- All games (for PC Engine CD) must be in cue/bin format. The generated json file will be saved using the same filename as the cue file, so be sure to also name that with the full title of the game

- When you run the `Generate Instance JSON Files` or `Update All` menu items, it will search through every directory in common and create a json file that can be launched by the core

- You can disable this process in Update All by setting `build_instance_jsons` to `false` in your settings file, if you don't want it to run every time you update.


### Jotego Beta Cores

Now that Jotego is releasing his beta cores publicly (and requiring a beta key to play them), you can just drop the `jtbeta.zip` file from patreon onto the root of your sd card and run Update All, and it will automatically copy the beta key to the correct folders for the cores that need it. It also will let you install the yhe cores directly from the updater, now. Make sure you don't rename the file, it's going to look for exactly `jtbeta.zip`

  

### How to build game and watch roms that are compatible with the pocket

Create 2 new folders.

`/Assets/gameandwatch/agg23.GameAndWatch/artwork` and `/Assets/gameandwatch/agg23.GameAndWatch/roms`

  

Place your `[artwork].zip` files into the artwork folder and your `[rom]`.zip files into the roms folder

  

Should look like this:

  

```

/Assets/gameandwatch/agg23.GameAndWatch/artwork/gnw_dkong.zip

/Assets/gameandwatch/agg23.GameAndWatch/roms/gnw_dkong.zip

```

  

Now just run the menu option in the updater and it will build your games

  

## Troubleshooting

 - Slow asset downloads? Try toggling `use_custom_archive` to true, in your settings.

  

 - If you run the update process and get a message like `Error in framework RS: bridge not responding` when running a core, try to run the updater in a local folder on your pc, and then copy the files over to the sd card afterwards. I'm not entirely sure what the issue is, but I've seen it reported a bunch of times now and running the updater locally seems to help.

 - I keep getting a `Missing ROM ID [1]` message when trying to launch arcade games in the _alternatives folder.

    Check your settings and make sure you have `Skip Alternative ROMS` turned off.
  

## Submitting new cores ##

You can submit new cores here [https://github.com/openfpga-cores-inventory/analogue-pocket](https://github.com/openfpga-cores-inventory/analogue-pocket)

  

## Credits ##

  

Thanks to [neil-morrison44](https://github.com/neil-morrison44). This is a port built on top of the work originally done by him here [https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8](https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8)

  

Special thanks to [RetroDriven](https://github.com/RetroDriven/) for maintaining the arcade rom archive.

  

And [dyreschlock](https://github.com/dyreschlock/pocket-platform-images/tree/main/arcade/Platforms) for hosting the updated platform files for Jotego's cores


And [espiox](https://github.com/espiox) for maintaining the game & watch rom archive

  
And if you're looking for something with a few more features and a user interface, check out this updater. [https://github.com/RetroDriven/Pocket_Updater](https://github.com/RetroDriven/Pocket_Updater)

  

Or if you want something cross platform that will run on a mac or linux: [https://github.com/neil-morrison44/pocket-sync](https://github.com/neil-morrison44/pocket-sync)
