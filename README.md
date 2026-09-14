# Bisheim 2.0 Launcher

Windows launcher for the Bisheim Valheim server.

Repository: https://github.com/Quakesz/Bisheim2pt0

## Development status

The source builds and its 12 automated regression checks pass. It is not ready for player
installation: the manifest URL and client-package URL remain placeholders, and a real modded
launch has not yet been tested.

## Build and test

Requires Windows and the .NET 8 SDK.

```powershell
dotnet build Bisheim2pt0/Bisheim2pt0.csproj -c Release
dotnet run --project Bisheim2pt0.Tests/Bisheim2pt0.Tests.csproj -c Release
```

See [launcher documentation](Bisheim2pt0/README.md) for implemented behavior and limitations.
See [client package policy](Bisheim2pt0/CLIENT-PACK-POLICY.md) for included/excluded mods.

## License

The launcher uses the GNU GPL v3 license selected in this repository's existing LICENSE file.
Retain that LICENSE file when importing these files. Third-party mods and runtimes remain under
their respective licenses; this source upload contains no mod binaries or game files.

## Distribution plan

Host a validated client ZIP as a versioned GitHub Release asset, with its SHA-256 hash and URL
in the public manifest. Configure the launcher manifest URL once the manifest and package are
actually available. No release assets or production manifest have been published yet.
