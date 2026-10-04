Status: Done (2026-10-04): S1–S4 built; "Later" is open

# Modular airship builder: build your own ship, save it, fly it

## Why
Building your own airship becomes a core loop: design a ship in the shipyard, fly it on a voyage, come back and
improve it. Today the ship is one hand-made model (`Assets/Prefabs/Airship/Airship_Prefab.prefab`, one collider)
furnished with fixtures at fixed ship-local poses. The old build mode was removed on 2026-09-27 (`trust-fixes.md`):
it had no catalog, no validation, and clients spawned any prefab by name. That plan left the way back open: "an
installer, a buildables catalog and server validation". This plan is that way back, starting with the smallest
slice that later slices can grow on.

**The design file is the product.** Everything else (the builder UI, the ship in the world, its stats) is built
from one plain data model, the `ShipDesign`. Getting that model and its file format right is the main goal of the
MVP; the builder can stay rough.

## What exists to build on
- **Fixtures** (`Core/Domain/Features/ShipFixtureDefinition.cs`, `Core/Ship/Fixtures/ShipFixtureSpawningUseCase.cs`):
  a networked prefab at a ship-local pose, spawned and parented by `ModuleSpawningService` with the local-pose rule
  (CODE_MAP traps). A placed *functional* part (the helm) is the same thing with its pose taken from a design.
- **Sky islands** (`sky-islands.md`): every peer builds geometry from replicated data, and only the data replicates.
  Structural parts work the same way.
- **The VoyageState fixture** (`Features/Voyage/VoyageNetworkMediator.cs`): replicated state carried by a fixture,
  so no shared prefab is edited.
- **Per-scene ship prefab**: each scene's `ProjectLifetimeScope._airshipPrefab` (the test range swaps in
  `TestShip_Prefab`). A bare ship root for designed ships needs no core change.
- **Entities**: `EntityIds.Derive` and `PresetEntityId` (`network-entities.md`), the hooks a saved ship needs to keep
  its ids across sessions.
- **Packages**: `com.unity.nuget.newtonsoft-json` 3.2.2 is already in `Packages/manifest.json`.

## The data model (the heart of the plan)

### `ShipDesign`: plain C#, no Unity objects
- `FormatVersion` (int): the version of the file schema, written on every save.
- `Name`, `Author` (strings; author is display-only).
- `GridCellSize` is **not** stored: the grid is a game constant. A file with a different cell size would be a
  different format version.
- `NextPartInstanceId` (int): a counter, so every placement keeps a stable id across edits.
- `Parts`: a list of placements:
  - `InstanceId` (int, unique in the design, never reused). Later per-part state (damage, wiring, cannon settings)
    and entity ids (`EntityIds.Derive(shipId, InstanceId)`) hang off it. It costs nothing now and keeps saves
    stable later.
  - `PartId` (string, e.g. `"hull.block.wood"`): the stable identity of a part type. It is never an asset name,
    asset GUID or Unity instance id, so renaming or moving an asset never breaks a save.
  - `Cell` (three ints): the part's origin cell on the ship grid. Integers keep the format exact, diffable and
    the same on every peer.
  - `Orientation` (byte, 0–23): one of the 24 axis-aligned rotations. The MVP only uses the four yaw turns
    (0–3), but the format allows all 24, so allowing walls and ceilings later needs no migration.
- The ship's origin is cell (0,0,0), which is where the ship root sits.

### `ShipPartDefinition`: a ScriptableObject in core
Core, next to `ShipFixtureDefinition`, so any feature can contribute parts without referencing the builder (the helm
feature contributes the helm part through `FeatureInstaller.IExtension<ShipPartDefinition>`, like fixtures).
- `PartId` (string, validated unique across the catalog, like `ItemDefinition.Id`). A rule test checks uniqueness
  and format.
- `Footprint`: the cells it occupies relative to its origin, before rotation.
- `Visual` prefab: mesh and colliders, no `NetworkObject` (structural parts).
- `NetworkedPrefab` (optional): a functional part spawned as a fixture at the design pose (the helm).
- `Category`, `DisplayName`; stats (`Mass`, `Hull`, `Thrust`, ...) come in S4.

### Two encodings of one model
| Use | Encoding | Why |
|---|---|---|
| Files (save, load, share, built-in designs) | JSON, `*.ship.json` | Human-readable and diffable in git. Shared designs can be read and fixed by hand. |
| Network (server → every peer) | Compact binary, `INetworkSerializable` | A part-id palette (each distinct `PartId` once) plus per placement: palette index, cell, orientation, instance id. About 8 bytes a part. |

Both live behind one codec interface (`IShipDesignCodec`: `Encode`/`Decode` to text) and the network struct, with
round-trip tests between them. Gameplay code only ever sees `ShipDesign`.

### Versioning and robustness rules
- **Every file carries `formatVersion`.** Loading runs pure migrations step by step (`v1 → v2 → …`) before
  decoding (`ShipDesignMigrator`). Each format change adds one migration and one **golden file**: a committed
  sample of the old format in `Assets/Tests/EditMode/ShipDesigns/` that must still load. Old saves stay tested
  forever.
- **A newer file than the game understands** is refused with a clear message, never half-loaded.
- **Unknown part ids are kept, not dropped.** The validator reports them; the assembler skips them with a warning;
  saving keeps them. A design made with a later part set survives a round trip through an older build.
- **Unknown JSON fields are preserved**, at the top level and on each part, for the same reason.
- **Canonical order and a content hash.** Encoding sorts parts by `InstanceId`, so the same design always produces
  the same bytes. `ShipDesignHash` (a stable integer hash of the canonical form, not `GetHashCode`) lets scenarios
  and the server compare designs cheaply. Host and client must report the same hash.
- **Limits** (`ShipDesignLimits`): max parts, max grid extent, max file size. They are checked on load before
  anything is built, so a hostile or broken file cannot stall the server.

### Validation: a pure processor, run by the server
`ShipDesignValidator` returns a list of problems (not a bool), so the builder can show them:
- no two parts overlap (rotated footprints on the grid);
- every part connects to the core part (flood fill over face-adjacent cells);
- exactly one core part (the helm in the MVP);
- every `PartId` is known (unknown ones are reported but do not block saving);
- within `ShipDesignLimits`.

The server validates every design before it builds or replicates one. Clients never send designs in the MVP.

### Edits are commands
The builder changes a design only through `ShipDesignEdit` values (`Place(partId, cell, orientation)`,
`Remove(instanceId)`), applied by a pure `ShipDesignEditProcessor`. That gives undo/redo for almost nothing. It is
also exactly what co-op building will send to the server later (a client proposes an edit, the server validates and
applies it), so the builder does not need a rewrite to go multiplayer.

## Shape of the feature
Two feature assemblies, so a playtest can fly designed ships without loading the builder UI:

- **`TinCan.Features.ShipDesigns`** (`Assets/Scripts/Features/ShipDesigns/`): the model, codec, migrator,
  validator, edit processor, part catalog, the assembler, and replication. It references Core.Domain, Ship, NGO,
  VContainer and Newtonsoft (`Newtonsoft.Json.dll` as a precompiled reference).
  - `ShipPartCatalog` (`IShipPartCatalog`): the parts every installer contributes.
  - `ShipDesignStore` (`IShipDesignStore`): lists, loads and saves `*.ship.json` in
    `Application.persistentDataPath/Ships/`, plus read-only built-in designs (TextAssets in
    `Assets/Settings/ShipDesigns/`, e.g. `Starter.ship.json`). It reads files only, with no Unity serialization of
    the model.
  - `ShipDesignNetworkMediator` on a `ShipDesign` fixture: a server-written `NetworkVariable` holding the network
    form of the design. Late joiners get it with the spawn.
  - `ShipAssemblyUseCase` (every peer): when a ship's design changes, it builds the structural parts as plain child
    objects (visuals + colliders) under the ship, diffing by `InstanceId`. On the server it also spawns functional
    parts through `IModuleSpawningService` at the design pose.
  - `ShipDesignFeatureInstaller`, `ShipDesignsConfig` (which design to fly: a built-in, or a file name; a
    `-shipDesign <name>` launch argument overrides it).
- **`TinCan.Features.Shipyard`** (`Assets/Scripts/Features/Shipyard/`): the builder. It references ShipDesigns,
  Input and UI.

**Bare ship prefab:** `Assets/Prefabs/Airship/ModularShip_Prefab.prefab` is the ship root
(`AirshipNetworkMediator`, `EntityNetworkMediator`, `AirshipControllerView`) with no hull model. Scenes that fly
designs select it in their scope. `Airship_Prefab` and its scenes stay as they are.

## Slices

### S1: Design data and files (no visuals, no networking)
- `ShipDesign`, `ShipPartDefinition` (core), `ShipPartCatalog`, the JSON codec, migrator scaffold (v1 only),
  validator, edit processor, hash, limits, `ShipDesignStore`.
- Three parts as assets: `hull.block` (1 cell), `hull.deck` (a 1-cell walkable floor), `core.helm` (contributed by
  the Helm installer, pointing at `Prefabs/Airship/Parts/HelmStation`).
- A built-in `Starter.ship.json` (a small deck with the helm), written by an Editor builder
  (**TinCan > Dev > Ship Designs > Build Assets**, like the other asset builders).
- Tests: JSON round trip, network round trip, JSON ↔ network equality, the golden v1 file, unknown part ids and
  fields survive a round trip, a newer version is refused, limits, every validator rule, edit apply and undo, hash
  stability across part order.
- No scenario: nothing runs in Play yet. S2 adds the first, as the AGENTS.md rule asks of a feature slice.

*Outcome (2026-10-04, unattended):*
- Built as planned. `ShipPartDefinition` is in `Core/Ship/Parts/`; everything else is in
  `Features/ShipDesigns/`. The binary network form is `ShipDesignBinaryCodec`: plain bytes, so S2 only wraps it for NGO.
- Grid geometry: cell (0,0,0) is centred on the ship root, and a cell's walkable surface is its top face. A deck tile
  is a 0.2 m slab flush with the top of its cell; a part's `PivotOffset` places prefabs whose pivot is at their base
  (the helm: (0, -0.5, 0)).
- The helm part is 3 × 2 × 3 cells: the stand mesh is about 4.5 × 2.2 × 3.9 m.
- The starter design is a 5 × 9 deck over a keel, with gunwales and the helm at the stern: 85 parts.
- Unknown fields are kept by reading the file as a JSON tree and keeping the leftover fields as raw JSON, not through
  `[JsonExtensionData]`. Same effect, and the model stays free of Newtonsoft types.
- Tests: 9 new files, about 70 cases, including the golden v1 file (`Assets/Tests/EditMode/ShipDesigns/`), a pinned
  hash, and the shipped starter checked against the installed parts. EditMode suite 858/858.

### S2: Fly a design (the first playable slice)
- The host loads the configured design, validates it, and writes it to the `ShipDesign` fixture. Every peer
  assembles it under `ModularShip_Prefab`. The helm is spawned from the design.
- Players board, stand on the deck, take the helm and fly. Islands still push the ship out (its colliders are the
  built parts).
- New test scene `Test_Shipyard` and profile `Profile_Test_Shipyard`: Ship, Helm, Stations, ShipDesigns, Boarding.
  No fuel, cannon or damage yet, because their fixtures sit at fixed poses that a custom hull doesn't match (see
  "Later").
- Scenario `DesignedShipFlies` (solo, host + client): the client sees the same design hash and part count as the
  host. A player on the deck stays on it while the helm steers. A late-join variant gets the design with the spawn.
- To check while building: the humanoid's moving-ground and boarding paths (`AirshipBoardingPose`) work with built
  colliders (layers, the respawn point).

*Outcome (2026-10-04, unattended):*
- The ShipDesignState fixture (`ShipDesignNetworkMediator`) replicates the design as one server-written
  `NetworkVariable` (`ShipDesignBlob`: hash + network bytes). Reliable messages are streamed across packets in this
  NGO/UTP version (`BatchedSendQueue.FillWriterWithBytes`), so the transport's 6 KB payload size does not cap a design.
- `ShipDesignUseCase` (server) picks the design: `IShipDesigns.Select` (for the shipyard's Launch), else
  `-shipDesign <name>`, else `ShipDesignsConfig.DefaultDesign`. An invalid design is refused and the ship stays bare.
- `ShipAssemblyUseCase` (every peer) builds the structure under a `DesignedHull` child, diffed by instance id, and fits
  the ship's `ParentLocalSpaceVolume` box to the design. The server spawns functional parts (the helm) once each.
  Parts removed from a design while in play are not despawned yet (nothing changes a design in play before S3).
- **Core change:** `IShipFixtureFilter` (`Core/Ship/Fixtures/`), asked by `ShipFixtureSpawningUseCase`. With designs
  loaded, a fixture whose prefab a part spawns (the helm) is kept off the ship.
- `ModularShip_Prefab` is a copy of `TestShip_Prefab` without its geometry. The test range builder has a per-area ship
  override and `BuildArea` (one scene, so the other test scenes are not regenerated).
- Profile: Stations, Helm, ShipDesigns on top of `Profile_Test_Core`. Boarding was left out: the scenario places the
  player itself.
- **Starter design change:** the helm moved to midships. At the stern there was no deck behind the wheel, so the
  helmsman had nowhere to stand. The first scenario run caught this.
- Scenario `DesignedShipFlies` passes solo and host + client (Lag100). The client builds the same hash, stands on
  the designed deck, takes the design's helm, steers, and is still on the ship after leaving the helm. The client
  joins after the design is applied, so it receives the design with the spawn, as a late joiner would.
- EditMode suite 874/874.

### S3: The shipyard (the builder)
- A **Shipyard** mode from the main menu: solo and offline, with an orbit camera around the grid, a ghost of the
  selected part, and place / remove / rotate / undo. Input is a `Context_Shipyard` with handlers (`INPUT.md`), never
  key polls. Placement aims through targeting or a grid ray in the feature; no `Camera.main` raycast against a
  hard-coded layer (one of the reasons the old build mode was removed).
- The preview uses the same assembler as the game, so what you build is what flies.
- A parts list, validator problems shown inline, and Save / Load / New against `IShipDesignStore`.
- **Launch** hosts a session with the current design.
- Scenario `ShipyardRoundTrip`: scripted edits, save, clear, load, same hash. Then launch, and the client sees that
  hash.

*Outcome (2026-10-04, unattended):*
- `TinCan.Features.Shipyard`. Main menu > **Shipyard** (offline) opens it on the starter design. Controls: LMB place,
  RMB remove, Q/E change part, R turn, hold MMB and drag to orbit, wheel to zoom, Ctrl+Z/Ctrl+Y to undo and redo,
  Esc for the menu. The menu has Name, Save, Load by name, New ship, Launch, Leave shipyard, Resume. The HUD shows
  the design, its part count, whether it can fly (the first blocking problem), the selected part and the last message.
- **Two core seams, both generic:**
  - `InputContextActivation.WhileOpened` with `IInputContextSwitch`, for a mode that no possession, tag or menu
    describes;
  - `IMainMenuRows`: a feature's installer adds main-menu rows, which exist only where the feature is loaded
    (`MainMenuComposition`).
- **The preview builds with the same `ShipHullAssembler` as ships in play** (extracted from `ShipAssemblyUseCase`).
  Part copies are stripped of every script before they wake (`ShipyardPrefabs`), so the networked helm can be shown
  offline.
- Aiming: a ray from the shipyard camera through the cursor, against the preview's colliders. The new part goes
  beside the face hit and Remove takes the part behind it. Over empty space the ray meets the build floor.
- **Launch:** offline, it hosts a session whose ship is built from the design (`IShipDesigns.Select`). As the host, it
  rebuilds the ships in play (`TryApply`), which the scenario uses.
- Found by the first play run: a dependency cycle (the menu system needs every menu command, the shipyard's
  commands need the shipyard, and the shipyard needed the menu system). The fix: the shipyard holds no menu; its
  commands and the Esc handler open and close menus. The EditMode suite cannot catch a cycle, because
  `FeatureProfiles_CanBuildTheirServices` registers services but does not build them.
- Scenario `ShipyardRoundTrip` passes solo and host + client. The host opens the shipyard during the session, places
  two blocks, saves "Scenario Ship", starts a new ship, loads the saved one back and launches. Host and client
  rebuild the ship in play with 87 parts. The scenario deletes its saved file at the end.
- Not built: picking a design from a list (the menu loads by name; Esc lists the saved names in the HUD), deleting
  saved designs from the menu, mouse-wheel layer stepping (the floor is layer 0; stack by clicking faces).

### S4: Parts matter (stats)
- Part stats: `Mass`, `Hull`, `Lift`, `Thrust`. A pure `ShipStatsProcessor` turns a design into the ship's max
  speed, turn speed and max health. The ship's `AirshipAttributeSet` and `HealthAttributeSet` take those values as
  base values on the server (a seam in `Core/Ship` like the pilot seam, so core doesn't know about designs).
- The validator gains rules: at least one engine; lift ≥ mass.
- The shipyard shows the stats live. The first parts that make choices interesting: engine, balloon, armour block.

*Outcome (2026-10-04, unattended):*
- `ShipPartStats` (Mass, Lift, Thrust, Hull) on `ShipPartDefinition`. `ShipStatsProcessor`: speed = thrust ÷ mass ×
  `SpeedPerThrustOverMass` (24), capped at 30 m/s. Turn = 45°/s × (600 ÷ mass), clamped to ×0.3–×1.5. Max health =
  hull, at least 100. Tunables in `ShipDesignsConfig.Flight`.
- **Core change:** `IAirshipTuning` (`Core/Ship/`), implemented by `AirshipNetworkMediator`. The server sets the
  ship's base flight speed, turn speed and health (filled) when a design is applied; they replicate as attributes.
- New validator rules: `TooHeavy` (lift below mass) and `NoThrust`.
- New parts: `hull.armour` (mass 30, hull 120), `prop.engine` (1 × 1 × 2 cells, mass 40, thrust 400),
  `lift.balloon` (3 × 2 × 3 cells, mass 15, lift 400). Existing parts: block (10, hull 25), deck (3, hull 5),
  helm (30, hull 100).
- The starter gains an engine off the stern and a balloon either side: 88 parts, mass 625, lift 800, thrust 400. It
  flies at 15.4 m/s and turns at 43°/s; the test ship flew at 15 and 45. "New ship" is 29 parts and flies too.
- The shipyard HUD has a Flight line ("mass 645, lift 800, thrust 400: 14.9 m/s, turns 42 deg/s, hull 1410").
- `DesignedShipFlies` checks the ship's replicated top speed (15.36 m/s) on host and client.
- Found by a play run: rerunning the ShipDesigns asset builder replaced `Profile_Test_Shipyard`'s installer list and
  dropped the Shipyard. Both builders now only add, and `StarterShipDesignTests` checks the profile.
- **Numbers are first guesses** and need a playtest: how much a balloon lifts, how fast big ships should be, and
  whether hull should come from every block.

## Later (out of the MVP; the model above is meant to support it)
- **Functional fixtures become parts.** Fuel system, cannon stations, repair rack and damage sockets are placed in
  the design instead of at fixed poses. This needs a decision on how installer fixtures and design parts coexist
  (likely: a ship with a design spawns only design parts, and features contribute part definitions instead of
  fixtures).
- **Co-op building**: the crew builds in a drydock on the live ship. Clients send `ShipDesignEdit`s through a server
  RPC with limits and validation; the server applies them to the replicated design.
- **Players bring their own design**: a client uploads a file to the host, with the same limits and validation.
- **Part damage**: blocks break off, keyed by `InstanceId`.
- **The meta loop**: voyages pay out materials, parts cost materials, unlocks per part. Needs a persistent player
  profile, which is a separate plan.
- **Performance**: merge structural meshes and colliders (greedy box merge) once ships pass a few hundred parts.

## Decisions (developer, 2026-10-04)
1. **Building is offline and solo first** (S3 as written). The co-op drydock comes later, on the same data and edit
   commands.
2. **1 m grid cells.** The deck is one cell; players (about 1.8 m) fit under a 2-cell ceiling.
3. **JSON through Newtonsoft** (`com.unity.nuget.newtonsoft-json` 3.2.2, already installed). System.Text.Json was the
   first choice, but it is not in Unity's game runtime (6000.4.5f1, Mono `unityjit`/`unityaot`; the Editor's copies
   are tooling only). It would come in as plugin DLLs whose dependencies (`Microsoft.Bcl.AsyncInterfaces`,
   `System.Runtime.CompilerServices.Unsafe`) clash by name with the ones `com.unity.burst` ships. The codec sits
   behind `IShipDesignCodec`, so a later switch touches one class and the golden files prove it.
4. **New test scenes first.** Designed ships fly only in `Test_Shipyard` until "functional fixtures become parts"
   lands; then the starter design replaces `Airship_Prefab` in the main game.

## Docs to update when built
`CODE_MAP.md` (feature index rows for ShipDesigns and Shipyard; "Where does X live": ship designs and save files),
`ARCHITECTURE.md` §7 (ships are built from designs; the design replicates and peers assemble it),
`NETWORK_TEST_HARNESS.md` (the new test scene).
