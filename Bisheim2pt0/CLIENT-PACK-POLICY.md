# Client package policy

Bisheim clients receive only mods required for normal play. Administrative and server-hosting
components stay on the dedicated server.

## Included

- BepInEx runtime and bootstrap files
- PlantEverything
- Better Archery
- Equipment and Quick Slots
- Jötunn
- Transmog
- Valheim Plus
- Corresponding shared configuration and asset files

## Excluded

- Server Devcommands and all of its configuration/YAML files
- WebMap and all generated or static WebMap content
- Server cache and log files

Future changes are published as a new version of the Bisheim2pt0 Thunderstore modpack.
Its dependency list selects mod package versions. The launcher downloads packages from
Thunderstore and preserves existing local configuration files. New shared configurations
must be reviewed and added to the modpack separately; the current release has none.
