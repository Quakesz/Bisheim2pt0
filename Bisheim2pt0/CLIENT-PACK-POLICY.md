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

Future changes are made by publishing a new client ZIP, changing the package URL/hash/version in
the hosted manifest, and incrementing the top-level manifest version. The launcher itself does not
need to be recompiled.
