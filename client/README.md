# Client

## Building

* Requires .NET Framework 4.6.2 SDK
* Create a `References` directory and copy the BepInEx.dll from your modded EFT install (`EFT/BepInEx/core/BepInEx.dll`) into that folder
* Do the usual build things, nothing complicated.
* Copy `bin/{BuildConfiguration}/RemotePlugins.dll` to your modded EFT install `EFT/BepInEx/patchers/`

## Installing

* From the zipped release, the client files are in the `BepInEx` directory. Ensure that `RemotePlugins.dll` ends up in `<ModdedEFTFolder/BepInEx/patchers`.

## Usage

* No other action should be necessary

## Notes

* This mod preempts the game's loading process which means that it will increase the amount of time required to start, but by a tiny amount. The time it takes depends on the need for an update, the server upload speed, the client download speed, and the client's speed in unpacking the files. In my testing, the first time setup added ~5 seconds with ~850 files @ 65mb in my `BepInEx/plugins` directory, a gigabit connection, and low end server with a gigabit connection. Subsequent starts added <1 second.
