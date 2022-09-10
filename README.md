[![Current Release](https://img.shields.io/github/v/release/mattpannella/pocket_core_autoupdate_net?display_name=tag)](https://github.com/mattpannella/pocket_core_autoupdate_net/releases/latest)

A free utility for updating the openFPGA cores, and firmware, on your Analogue Pocket. 
I will be maintaining the list of available cores, which can be found in the `auto_update.json` file. But if I ever fall behind, or just stop keeping it up to date, you can manually add to it by following the format of the existing cores. It currently only supports github repos that uses the releases feature and release via zip file. If/when future openFPGA developers release their cores via other platforms, I'll do my best to implement support for those as well.

A complete list of available cores can also be found here: https://joshcampbell191.github.io/openfpga-cores-inventory/analogue-pocket

----
### Instructions
To run the updater, all you need to do is put the executable file for your platform (windows, mac os, or linux) and auto_update.json in the root of your sd card and run the program.

#### Advanced Usage
The updater currently accepts 2 command line parameters. I will probably add more options in the future
```
 -u, --update    Automatically download newest core list without asking.
 -p, --path      Absolute path to install location
```
example:
`
/path/to/pocket_updater -u -p /path/to/sdcard/
`

----
This is a port of the work initially done by [neil-morrison44](https://github.com/neil-morrison44) here https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8 with a few tweaks to suit my needs
