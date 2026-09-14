# Bisheim 2.0 Launcher

Windows-first launcher for installing, repairing, updating, and starting the Bisheim Valheim modpack.

## Current MVP

- WPF interface with Install & Play and Repair actions
- Remote JSON manifest and modpack version comparison
- Add or remove manifest packages without recompiling the launcher
- Managed-file inventory removes obsolete mods on update without touching unrelated files
- SHA-256 verification before installing packages
- ZIP-slip protection during extraction
- Isolated profile under `%LOCALAPPDATA%\Bisheim2pt0\profile`
- Steam launch with Doorstop arguments pointing to the isolated BepInEx profile
- Default and custom Steam library detection (modern and legacy libraryfolders.vdf)
- Refuse conflicting game-folder loaders; leave the bootstrap disabled for ordinary Steam launches
- Direct connection to `srv781780.hstgr.cloud:2456` using Valheim's `+connect` argument

## Build

Install the .NET 8 SDK on Windows, then run:

```powershell
dotnet publish .\Bisheim2pt0.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true
```

The output will be under `bin\Release\net8.0-windows\win-x64\publish`.

## Before the first player test

1. Host `manifest.json` and the modpack ZIP at public HTTPS URLs.
2. Replace `LauncherSettings.ManifestUrl` with the real manifest URL.
3. Put the profile contents in the ZIP, including BepInEx, plugins, and shared configs. A package
   may use `stripPrefix` when its ZIP has one containing folder.
4. Generate the package hash with `Get-FileHash .\package.zip -Algorithm SHA256`.
5. Put that hash in the manifest.

`manifest.current.json` is wired to the sanitized `Bisheim-client-pack-0.1.0.zip` hash. Replace
its placeholder URL after hosting that package. See `CLIENT-PACK-POLICY.md` for the enforced
server/client separation used when assembling the package.

## Next build targets

- Verify a real modded game launch with the hosted client package
- Validate and host the sanitized client package and production manifest
- Download progress by bytes and cancellation
- Preserve player-editable config files during updates
- Server status and direct-connect button
- Launcher self-update and code signing

## Local continuation update

Steam discovery, isolated Doorstop launch arguments, loader conflict checks, and UI busy/error
handling are implemented. Missing System.IO imports in the original source were also corrected.
The WPF project builds on Windows with .NET SDK 8.0.425 (zero warnings/errors).

The launcher places winhttp.dll and a disabled doorstop_config.ini beside valheim.exe on first
launch. It points Doorstop at the profile's BepInEx.Preloader.dll through Steam arguments.
It recognizes Doorstop 3 and 4 configuration key names. It refuses to overwrite a different
loader or existing configuration. If a future pack changes the loader DLL, replacement currently
requires manually backing up/removing the old loader; automated bootstrap upgrades are pending.
Mods/configs remain under the profile. Game-folder write access is required for bootstrap setup.
An already running Valheim process blocks installs and launches.

Ordinary Steam launches leave this bootstrap disabled unless the player has configured their own
Doorstop launch overrides. Existing Steam launch options and real game behavior still need a
player test. No actual game or Steam process was started during automated validation.

## Regression checks

The sibling Bisheim2pt0.Tests console project uses temporary fake Steam/game/profile folders;
it does not load DLLs or start Steam. Run:

```powershell
dotnet run --project ..\Bisheim2pt0.Tests\Bisheim2pt0.Tests.csproj
```

Twelve checks cover library formats, missing games, paths with spaces, both Doorstop argument
formats, direct-connect arguments, disabled bootstrap defaults, repeated setup, conflicting
loader/config preservation, and missing preloader rejection.

Implementation references:
- https://github.com/NeighTools/UnityDoorstop#cli-arguments
- https://github.com/NeighTools/UnityDoorstop/tree/legacy
- https://github.com/BepInEx/BepInEx/blob/v5.4.23.2/BepInEx.Preloader/Entrypoint.cs

Still required before distribution: real hosted URLs, verified client pack contents and compatible
mod versions, config preservation/backup and transactional update handling, and an end-to-end
Windows play test. The existing manifest/package placeholders remain unchanged.
