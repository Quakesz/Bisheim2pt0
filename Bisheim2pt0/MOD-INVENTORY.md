# Current client mod inventory

Sources: `Client-Side Mods.zip` and `01 BepInEx - Server Master.zip`, received
September 15, 2026. The sanitized launcher package is `Bisheim-client-pack-0.1.0.zip`.

| Mod or dependency | Evidence in archive | Configuration included |
| --- | --- | --- |
| PlantEverything | `Advize_PlantEverything.dll` | `advize.PlantEverything.cfg` |
| Better Archery | `BetterArchery/BetterArchery.dll` | `ishid4.mods.betterarchery.cfg` |
| Equipment and Quick Slots | `EquipmentAndQuickSlots.dll` | `randyknapp.mods.equipmentandquickslots.cfg` |
| Jötunn | `Jotunn/Jotunn.dll` | No dedicated config included |
| Transmog | `Transmog.dll` plus translations | `Transmog.cfg` |
| Valheim Plus | `ValheimPlus.dll` | `org.bepinex.plugins.valheim_plus.cfg` |
The sanitized package also includes the BepInEx core, `winhttp.dll`, `doorstop_config.ini`, general
BepInEx configuration, translations, and the supplied sound assets.

## Deliberately excluded from clients

- Server Devcommands plugin and `server_devcommands.cfg`
- `alias.yaml`, `binds.yaml`, and `permissions.yaml`
- WebMap plugin, dependencies, web assets, generated map data, and configuration
- Server cache and `LogOutput.log`

## Adding or removing mods later

The launcher reads packages from the hosted manifest at runtime. Change the manifest version and
its package list; the launcher will download new packages, overwrite updated managed files, and
delete only previously managed files that no longer appear in the new package set. No launcher
recompile is required for ordinary mod-list changes.
