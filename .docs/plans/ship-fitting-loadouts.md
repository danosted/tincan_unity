Status: Approved (2026-10-15): slice 1 built; slice 2 (the shipyard's fitting tool) is open

# Ship fitting loadouts: a ship's stations saved beside its design

## Why
The developer wants the Galleon as the airship, with stations already in some of its sockets. The old airship
(`Prefabs/Airship/Airship_Prefab`) stays, since other scenes and people use it. What is mounted must be saved with the
ship, so a player who builds a ship and fits it out can save it and fly it again.

## Decisions (developer, 2026-10-15)
- **Fittings live in their own file**, linked to the design, never in the design file. Ship designs stay a builder
  concern; what goes in the sockets is another feature's (ShipSockets). This reverses nothing: the design format is
  unchanged (still version 1).
- **The link is the design key**: `<key>.fittings.json` beside `<key>.ship.json` (in `persistentDataPath/Ships/`, or
  next to the built-in under `Assets/Settings/`). The key is the design's name as the store names its file
  (`ShipDesignKeys.FromName`), which is what the shipyard saves under.
- **cheesed_scene flies the Galleon**; `cvg_airship_default` and `drm_cloud_environment` keep the old airship.
- **Stations:** four cannons (bow pair on the main deck, stern pair on the quarterdeck) and the tool rack in the hold.
  The fuel system, net rack, grapple rack and damage points are fixed-pose fixtures made for the old hull; they stay off
  the Galleon until they become fittings.
- **Two slices:** first the file, mounting it in play and the Galleon; then a fitting tool in the shipyard.
- **Building stays in the shipyard (2026-10-15).** Designed ships, the Galleon included, fly in the game; building
  and fitting them out happens only in the shipyard. Building in the game needs its own design: when it happens in a
  session, and a lot of UI.

## Slice 1 (built)
- *File* (`Features/ShipSockets/`): `ShipFittingLoadout` (socket → fitting id), `ShipFittingLoadoutFormat` (version 1,
  one line per fitting), `ShipFittingLoadoutJsonCodec` (refuses a newer version, a socket named twice, a missing id;
  whether the socket or fitting exists is checked when mounting, so a loadout survives a design edit that removed a
  part), `FileShipFittingLoadoutStore` (`IShipFittingLoadoutStore`: built-ins from `ShipSocketsConfig.BuiltInLoadouts`,
  saved ones beside the designs; no file means an empty loadout; built-ins cannot be overwritten).
- *In play:* `ShipFittingLoadoutUseCase` (server) waits until a ship stands built from its design, reads the design's
  name from the replicated design, loads that key's loadout and mounts it through `IShipFittings.Mount`, once per
  design. A fitting the ship cannot take is skipped with a warning. `ShipFittingLoadout.From(mounted)` turns what is
  mounted into a loadout, for saving in slice 2.
- *Which design a ship flies:* `ShipDesignPreset` (ShipDesigns) on a ship prefab names a design key and lists the
  fixed-pose fixtures kept off that ship (`ShipDesignPresetFixtureFilter`). Precedence in `ShipDesignUseCase`, per ship:
  the shipyard's Launch, else `-shipDesign`, else the ship's preset, else the config default. Ships without a preset
  (ModularShip_Prefab in Test_Shipyard) behave as before.
- *Assets* (built by **TinCan > Dev > Ship Sockets > Build Galleon Airship**): `Settings/ShipSockets/Galleon.fittings.json`,
  `Prefabs/Airship/Galleon_Prefab` (a variant of ModularShip_Prefab with the preset), `Settings/FeatureProfiles/Profile_Galleon`
  (Profile_FuelSandbox plus ShipDesigns and ShipSockets), and cheesed_scene's scope set to both.
- *Tests:* `ShipFittingLoadoutTests`, `ShipFittingLoadoutUseCaseTests`, `GalleonLoadoutTests`, preset cases in
  `ShipDesignUseCaseTests`. *Scenario* `GalleonLoadout` (Test_Shipyard): the galleon gets its four cannons on host and client.
  Test_Shipyard does not load ShipDamage, so the tool rack is skipped there (with a warning); `Profile_Galleon` loads it.

## Slice 2 (next): fit out in the shipyard
- A fitting mode in the shipyard: aim at a socket of the preview, pick a fitting, remove one; undo like part edits.
  The shipyard holds the loadout beside its design document (the builder still never edits fittings into the design).
- Save writes both files under the same key; Load reads both; Launch selects the design and hands the loadout to the
  server, which mounts it instead of the stored one.
- Removing a part drops the fittings on its sockets from the loadout.
- Open: deleting a design also deleting its loadout (`IShipDesignStore.TryDelete` does not know about it yet).
- Not planned for now: saving from play (`ShipFittingLoadout.From` exists for it), since building stays in the shipyard.

## Known limits
- The Galleon has no fuel system: with no tank, fuel neither burns nor stalls the engine. No damage points either, so
  nothing breaks and the tool rack has nothing to repair yet.
- Mounts survive a design change on sockets the new design still has (existing behaviour of `ShipFittingUseCase`); a
  loadout fitting for a socket that is taken is skipped.
- Socket ids are part instance ids in `Galleon.ship.json`. If `ShipDesignsAssetBuilder.GalleonDesign()` is renumbered,
  `GalleonShipDesignTests` and `GalleonLoadoutTests` fail until both files are rebuilt.
