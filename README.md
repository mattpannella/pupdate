## Oh Hi ##
[![Current Release](https://img.shields.io/github/v/release/mattpannella/pocket-updater-utility?label=Current%20Release)](https://github.com/mattpannella/pocket-updater-utility/releases/latest) ![Downloads](https://img.shields.io/github/downloads/mattpannella/pocket-updater-utility/latest/total?label=Downloads)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate/?business=YEERX89E75HQ8&no_recurring=1&currency_code=USD)

A free utility for updating the openFPGA cores, and firmware, on your Analogue Pocket. 
The update process will check for pocket firmware updates, openfpga core updates, and install any required BIOS files and arcade ROMS. You're on your own when it comes to console ROMs. 


A complete list of available cores can also be found here: https://openfpga-cores-inventory.github.io/analogue-pocket/

I can't (and don't want to) support old versions, so please make sure you download the latest release before submitting any issues.

## Instructions ##
If you just want to use this utility, do not clone the source repository. Just
download the [latest release](https://github.com/mattpannella/pocket-updater-utility/releases/latest/). Unzip it, put the executable file for your platform (windows, mac os, or linux) in the root of your sd card, and run the program.

At the main menu run `Configuration Wizard` to have it walk through the available settings for you.

#### Advanced Usage
CLI Parameters
```
 -p, --path      Absolute path to install location
 -a, --all       Extract all release assets, instead of just ones containing openFPGA cores.
 -c, --coreselector    Run the core selector.
 -f, --platformsfolder   Preserve the Platforms folder, so customizations aren't overwritten by updates
 -u, --update    Skip the main menu and just run the update process automatically
 -i, --instancegenerator    Skip the main menu and just run the instance json builder for supported cores (will overwrite all)
```
example:
`
/path/to/pocket_updater -a -p /path/to/sdcard/
`
#### Download Image Packs
This will present you with a list of available image packs and automatically download and extract it to the Platforms/_images directory for you

#### Core Selector
On your first run it will prompt you to select the cores you want tracked. After that initial run, you can run it again any time via the main menu. Or you can always run this again by setting `config.core_selector` to `true` in the settings json file, or if running from the cli you can use the paramater -c

#### Generating Instance JSON Files
 - Only supported by PC Engine CD, currently
 - Put your games in /Assets/{platform}/common
 - Each game needs to be in its own directory (and be sure to name the directory the full title of the game)
    - Example: /Assets/pcecd/common/Rondo of Blood
    - /Assets/pcecd/common/Bonk
    - etc
 - All games (for PC Engine CD) must be in cue/bin format. The generated json file will be saved using the same filename as the cue file, so be sure to also name that with the full title of the game
 - When you run the `Generate Instance JSON Files` or `Update All` menu items, it will search through every directory in common and create a json file that can be launched by the core
 - You can disable this process in Update All by setting `build_instance_jsons` to `false` in your settings file, if you don't want it to run every time you update.

### Settings
#### All settings can be modified in your `pocket_updater_settings.json` file

|                                          |                                  |                                                                                                                                                                                                      |
|------------------------------------------|----------------------------------|------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| Allow pre-release cores                  | coreSettings.{corename}.allowPrerelease                  | Set to `true` for any core want to download, even though it's still pre-release                                                                                                                        |
| Disable Firmware Downloading             | config.download_firmware         | Set to `false` if you don't want Update All to check for firmware updates                                                                                                                              |
| Disable Asset Downloading                | config.download_assets           | Set to `false` if you'd like to supply your own BIOS and arcade rom files, and don't want Update All to handle this.                                                                                   |
| Preserve Platforms Folder Customizations | config.preserve_platforms_folder | If you have any customizations to the Platforms folder, you can use this option to preserve them during the update process. Set to `true` in your settings file, or use `-f` as a command line parameter |
| Github Personal Access Token             | config.github_token              | If you're running up against the rate limit on the github api, you can provide your personal access token to the updater via the settings.                                                           |
| Disable Instance JSON Builder            | config.build_instance_jsons      | Set this to `false` if you don't want Update All to build instance JSON files.
| Delete Untracked Cores           | config.delete_skipped_cores      | `true` by default. Set to `false` if you don't want the updater to remove cores you don't select to track  

#### Troubleshooting
If you run the update process and get a message like `Error in framework RS: bridge not responding` when running a core, try to run the updater in a local folder on your pc, and then copy the files over to the sd card afterwards. I'm not entirely sure what the issue is, but I've seen it reported a bunch of times now and running the updater locally seems to help.

## Submitting new cores ##
You can submit new cores here https://github.com/openfpga-cores-inventory/analogue-pocket

## Credits ##

Thanks to [neil-morrison44](https://github.com/neil-morrison44). This is a port built on top of the work originally done by him here https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8

Special thanks to [RetroDriven](https://github.com/RetroDriven/) for maintaining the arcade rom archive.

And if you're looking for something with a few more features and a user interface, check out this updater. https://github.com/RetroDriven/Pocket_Updater

Or if you want something cross platform that will run on a mac or linux: https://github.com/neil-morrison44/pocket-sync
