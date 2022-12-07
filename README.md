## Oh Hi ##
[![Current Release](https://img.shields.io/github/v/release/mattpannella/pocket-updater-utility?label=Current%20Release)](https://github.com/mattpannella/pocket-updater-utility/releases/latest) ![Downloads](https://img.shields.io/github/downloads/mattpannella/pocket-updater-utility/latest/total?label=Downloads)
[![Donate](https://img.shields.io/badge/Donate-PayPal-green.svg)](https://www.paypal.com/donate/?business=YEERX89E75HQ8&no_recurring=1&currency_code=USD)

A free utility for updating the openFPGA cores, and firmware, on your Analogue Pocket. 
The update process will check for pocket firmware updates, openfpga core updates, and install any required BIOS files and arcade ROMS. You're on your own when it comes to console ROMs. 


A complete list of available cores can also be found here: https://joshcampbell191.github.io/openfpga-cores-inventory/analogue-pocket


## Instructions ##
If you just want to use this utility, do not clone the source repository. Just
download the [latest release](https://github.com/mattpannella/pocket-updater-utility/releases/latest/). Unzip it, put the executable file for your platform (windows, mac os, or linux) in the root of your sd card, and run the program.

#### Advanced Usage
The updater currently accepts 4 command line parameters. I will probably add more options in the future
```
 -p, --path      Absolute path to install location
 -a, --all       Extract all release assets, instead of just ones containing openFPGA cores.
 -c, --coreselector    Run the core selector.
 -f, --platformsfolder   Preserve the Platforms folder, so customizations aren't overwritten by updates
 -u, --update    SKip the main menu and just run the update process automatically
```
example:
`
/path/to/pocket_updater -a -p /path/to/sdcard/
`
#### Download Image Packs
This will present you with a list of available image packs and automatically download and extract it to the Platforms/_images directory for you

#### Core Selector
On your first run it will prompt you to select the cores you want tracked. After that initial run, you can run it again any time via the main menu. Or you can always run this again by setting `config.core_selector` to `true` in the settings json file, or if running from the cli you can use the paramater -c

#### Allow pre-release cores
You can edit your `pocket_updater_settings.json` file and set the `allowPrerelease` flag to `true` for any core want to download, even though it's still pre-release

#### Disable Firmware Downloading
Set `config.download_firmware` to `false` in your settings file

#### Disable Asset Downloading
Set `config.download_assets` to `false` in your settings file, if you'd like to supply your own BIOS and arcade rom files

#### Preserve Platforms Folder Customizations
If you have any customizations to the Platforms folder, you can use this option to preserve them during the update process.
Set `config.preserve_platforms_folder` to `true` in your settings file, or use `-f` as a command line parameter

#### Github Personal Access Token
If you're running up against the rate limit on the github api, you can provide your personal access token to the updater via the settings.
Edit your local copy of `pocket_updater_settings.json` and put your token in `config.github_token`

#### Troubleshooting
If you run the update process and get a message like `Error in framework RS: bridge not responding` when running a core, try to run the updater in a local folder on your pc, and then copy the files over to the sd card afterwards. I'm not entirely sure what the issue is, but I've seen it reported a bunch of times now and running the updater locally seems to help.

## Submitting new cores ##
You can submit new cores here https://github.com/joshcampbell191/openfpga-cores-inventory

## Credits ##

Thanks to [neil-morrison44](https://github.com/neil-morrison44). This is a port built on top of the work originally done by him here https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8

Special thanks to [RetroDriven](https://github.com/RetroDriven/) for maintaining the arcade rom archive.

And if you're looking for something with a few more features and a user interface, check out this updater. https://github.com/RetroDriven/Pocket_Updater

Or if you want something cross platform that will run on a mac or linux: https://github.com/neil-morrison44/pocket-sync
