Only GitHub repositories will work, and they need to be using the releases feature. It won't do a git pull on the repo, itself

It will try to compare your installed version to the one hosted on github, and download a newer version if available. if it can't figure out your locally installed version, it will just pull the newest one off github anyway

A complete list of available cores can be found here, which includes the info you need to add new cores to your auto_update.json file https://joshcampbell191.github.io/openfpga-cores-inventory/analogue-pocket

To run it, put the executable file and auto_update.json in the root of your sd card and run the script

----
This is a port of the work initially done by [neil-morrison44](https://github.com/neil-morrison44) here https://gist.github.com/neil-morrison44/34fbb18de90cd9a32ca5bdafb2a812b8 with a few tweaks to suit my needs