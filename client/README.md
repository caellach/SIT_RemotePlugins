# Client

## From source

* Requires .NET Framework 4.6.2 SDK
* Create a `References` directory and copy the BepInEx.dll from your modded EFT install (`EFT/BepInEx/core/BepInEx.dll`) into that folder
* Do the usual build things, nothing complicated.
* Copy `bin/{BuildConfiguration}/RemotePlugins.dll` to your modded EFT install `EFT/BepInEx/patchers/`

## Installing

* From the zipped release, the client files are in the `BepInEx` directory. Ensure that `RemotePlugins.dll` ends up in `<ModdedEFTFolder/BepInEx/patchers`.

## Usage

* After the first run, a default config file will be generated at `EFT/BepInEx/config/RemotePlugins.cfg`
* If the files retrieved from the server doesn't contain `BepInEx/plugins/StayInTarkov.dll` then this plugin will not modify anything. This is to prevent the server from wiping out folders before the client setup is complete.

## Configuration

* `number` Version - The version of the config file. If the version is different than the current version, then the config will try to update itself. The values that can't be read will be set to their default values.
* `bool` Debug - Shows more output. Not currently in use.
* `bool` KnownFilesOnly - If it should do hash checking against the built-in hash list and the `AllowedFileHashes` array in the config.
* `array<string>` AllowedFileHashes - A list of file hashes that are allowed to pass the known files checks.
* `UnknownFileHashAction` UnknownFileHashAction - enum, what should happen when a file hash is unknown.
  * "QUARANTINE" - Moves/Extracts the file to BepInEx/remoteplugins/quarantine/.
  * "DELETE" - Deletes the file if it exists & doesn't extract from the zip.
  * "WARN" - Extracts normally and outputs info in the log.

## Notes

* This mod preempts the game's loading process which means that it will increase the amount of time required to start, but by a tiny amount. The time it takes depends on the need for an update, the server upload speed, the client download speed, and the client's speed in unpacking the files. In my testing, the first time setup added ~5 seconds with ~850 files @ 65mb in my `BepInEx/plugins` directory, a gigabit connection, and low end server with a gigabit connection. Subsequent starts added <1 second.
