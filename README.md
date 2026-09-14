# Bisheim 2.0 Launcher

Windows launcher for the [Bisheim Thunderstore modpack](https://thunderstore.io/c/valheim/p/Bisheim2pt0/Bisheim2pt0/).

## Current behavior

- Reads the latest published Bisheim modpack version and resolves its dependency graph.
- Uses the exact top-level versions selected by the modpack. Older transitive requirements reuse those versions; conflicting newer requirements produce an error.
- Downloads the dependency ZIPs directly from Thunderstore and installs the current pack's Windows BepInEx layout into an isolated profile.
- Stages updates, validates the bootstrap files, and switches profiles with automatic rollback on activation failure.
- Preserves existing configuration files and retains the previous profile as a backup.
- Launches Valheim through Steam with the isolated Doorstop profile and the Bisheim server address.

## Build and test

Requires Windows and .NET 8 SDK.

```powershell
dotnet build Bisheim2pt0/Bisheim2pt0.csproj -c Release
dotnet run --project Bisheim2pt0.Tests/Bisheim2pt0.Tests.csproj -c Release
# Optional: download the published pack into temporary folders; never starts Steam or Valheim.
dotnet run --project Bisheim2pt0.Tests/Bisheim2pt0.Tests.csproj -c Release -- --live
```

## Validation and remaining work

Release build passes with zero warnings. Offline regression checks and a real Thunderstore download/extraction test pass. A real modded-game launch and server connection have not yet been tested. Mod compatibility is not established by successful extraction.

Read [launcher details](Bisheim2pt0/README.md) for update, backup, integrity, and layout limits.

## License

The existing repository LICENSE is GNU GPL v3 and remains unchanged. Mods and their runtimes retain their own licenses and are downloaded from their original Thunderstore packages.
