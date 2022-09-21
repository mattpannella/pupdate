## Oh Hi ##
[![Current Release](https://img.shields.io/github/v/release/mattpannella/pocket_core_autoupdate_net?label=Current%20Release)](https://github.com/mattpannella/pocket_core_autoupdate_net/releases/latest) ![Downloads](https://img.shields.io/github/downloads/mattpannella/pocket_core_autoupdate_net/latest/total?label=Downloads)

A free utility for updating the openFPGA cores, and firmware, on your Analogue Pocket. 
I will be maintaining the list of available cores, which can be found in the `pocket_updater_cores.json` file. But if I ever fall behind, or just stop keeping it up to date, you can manually add to it by following the format of the existing cores. It currently only supports github repos that uses the releases feature and release via zip file. If/when future openFPGA developers release their cores via other platforms, I'll do my best to implement support for those as well.

If you are a developer and want your core added to the list please feel free to add it to `auto_update.json` and make a pull request

A complete list of available cores can also be found here: https://joshcampbell191.github.io/openfpga-cores-inventory/analogue-pocket


## Instructions ##
To run the updater, all you need to do is put the executable file for your platform (windows, mac os, or linux) in the root of your sd card and run the program. It will prompt you to download the latest list of available cores before starting the update process.

#### Advanced Usage
The updater currently accepts 3 command line parameters. I will probably add more options in the future
```
 -u, --update    Automatically download newest core list without asking.
 -p, --path      Absolute path to install location
 -a, --all       Extract all release assets, instead of just ones containing openFPGA cores.
```
example:
`
/path/to/pocket_updater -u -p /path/to/sdcard/
`

#### Disabling Cores
You can edit your `pocket_updater_settings.json` file and set the `skip` flag to true for any core you don't want to be updated. It won't remove anything from your sd installs, it just skips that core during the update process

#### Github Personal Access Token
If you're running up against the rate limit on the github api, you can provide your personal access token to the updater via the settings.
Edit your local copy of `pocket_updater_settings.json` and put your token in config>github_token

## Submitting new cores ##
If you'd like to add your(or anyone else's) core to the list, here is the format used in pocket_updater_cores.json. Just create a pull request and I will get your change merged in.
```
{
   "repo":{
      "user":<github username. required>,
      "project":<github project name. required>
   },
   "name":<name of the directory the core gets installed to. example: Spiritualized.GBA. required>,
   "platform":<platform name. required>
}
```

## Credits ##
This is a port of the work initially done by [neil-morrison44](https://github.com/neil-morrison44) here https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8 with a few tweaks to suit my needs

Special thanks to [RetroDriven](https://github.com/RetroDriven/) for maintaining the arcade rom archive
