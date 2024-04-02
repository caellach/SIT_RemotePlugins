# Remote Plugins

## From source

* Copy this folder to the AKI Server into `user/mods/`
* Ensure that you have NodeJS installed and run `npm install` in this directory to install dependencies.
  * You could also do this on a different machine and copy the resulting `node_modules` directory to the server into this mod's directory

## Installing from zip

* From the zipped release, the server files are in the `user` folder. You can extract only this folder in where you have the AKI Server installed and start/restart the server.

## Usage

* When you first run the mod, it will create the necessary config and the `BepInEx` folder for you to place your files.
* It's recommended that you setup the client mods (those in the EFT `BepInEx/plugins/` directory) that you want first so you can ensure that they work as intended.
* All clients must be on the same EFT version. There is currently no check for this.
* Copy only the contents of the BepInEx directory that you intend to be shared between clients.
  * This almost always means only the `BepInEx/plugins` directory.
  * If you copy `BepInEx/config` then your players may be unhappy since those are intended to be per-client and the user's changes will be overridden.
* The copied version in the server BepInEx directory should mirror the structure of the original client BepInEx directory.
* If mods need to be retrieved to be deployed then it may take an extra couple of seconds to startup since this mod preempts the game's loading process. The time it takes depends on the server upload speed, the client download speed, and the client's speed in unpacking the files.

## Notes

* The server will automatically generate the necessary files from what's in the BepInEx directory for the client to download.
* You can force the server to rebuild the zip file by deleting `files/fileMap.json` in this mod's directory.
* This does have a nice side-effect that you can deliver SIT updates since it's a regular mod in the plugins directory.
