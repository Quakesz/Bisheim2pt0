# Launcher implementation notes

## Published modpack

https://thunderstore.io/c/valheim/p/Bisheim2pt0/Bisheim2pt0/

The launcher reads Thunderstore's experimental package API. Updating this modpack to a higher version publishes a new dependency selection for the launcher. Changes to the individual mods alone do not automatically replace the top-level versions chosen by the modpack. Server address remains srv781780.hstgr.cloud:2456 in LauncherSettings.

## Downloads and integrity

Downloads start at Thunderstore HTTPS package endpoints (which may redirect to its CDN). The API does not provide an independent expected SHA-256. The launcher records the hash of each downloaded archive and rejects a different hash when downloading a previously installed identical package version again. This is trust on first use, not signature verification or an independent first-download authenticity check. ZIP parse errors and unsafe paths abort before profile activation.

Limits: 150 selected packages, 1,000 resolver iterations, 512 MB per download, 30,000 entries and 2 GB expanded size per archive. Downloads time out. Unsupported third-party core/patcher/monomod routing is rejected for explicit review.

## Installation layout

Supports the seven packages in Bisheim2pt0 1.0.0: the official BepInExPack_Valheim Windows bootstrap and core, plugins at archive root, BepInEx/plugins, nested plugins folders, and config folders. Plugins are placed in stable author-package subfolders with relative assets retained. The modpack itself is processed last. Metadata-only modpacks add no game files.

This is not a complete implementation of every Thunderstore installer format. New mods that use custom installer declarations or unusual routes require validation before release. Routing reference: https://wiki.thunderstore.io/mods/packaging-your-mods

## Updates and backups

Profile: %LOCALAPPDATA%/Bisheim2pt0/profile.

Candidate profile is built beside the current profile. The previous profile becomes profile.rollback; older backups are retained with unique suffixes. Activation failures restore the old profile. If the program stops between moving the old profile away and activating its replacement, the next profile read recovers profile.rollback. Backups consume disk space and currently require manual cleanup.

Existing files under BepInEx/config are preserved, including obsolete configs. New config defaults are installed only if that path does not already exist. Server-provided config changes therefore do not override existing player configs. Obsolete managed non-config files are removed; unmanaged files are preserved, and conflicting unmanaged file replacements abort the update.

## Launch limitations

The launcher refuses existing foreign game-folder loaders/configs. The user's inspected Valheim installation already has a loader, so do not expect the launcher to overwrite it. Use a separate test installation or a deliberately backed-up loader transition. A real launch, BepInEx initialization, game compatibility, and server connection are still untested.

Ordinary Steam launches leave the launcher's bootstrap disabled. A bootstrap DLL upgrade may require manual replacement after backup. No game files or user saves are changed by automated tests.

The old manifest.example.json and manifest.current.json are legacy source examples and are no longer read by the launcher.
