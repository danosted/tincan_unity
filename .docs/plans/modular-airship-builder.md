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

## Follow-up (developer feedback, 2026-10-04)
"Parts should snap to existing pieces, not float; switch height levels with PgUp/PgDn. Bigger balloons, like a top
balloon floating above the deck."
- **Levels:** the shipyard has a working level (PgUp/PgDn, shown in the HUD). New parts aim at that level's floor,
  drawn as a see-through plane (`M_ShipyardLevel`). Remove still takes whatever part the cursor is on.
- **No floating parts:** a part must touch the ship (share a face with a part already there); only the first part of an
  empty design goes anywhere. Aim near the ship but not touching, and the ghost snaps to the nearest touching spot on
  the same level within 2 cells (`ShipyardSnapProcessor`). Farther away it stays red and a click explains why.
  `IShipyard.PlaceAt` enforces the same rule.
- **Top balloon:** `lift.envelope` (5 × 3 × 7 cells, mass 40, lift 1200), held up by `hull.mast` posts (1 cell, mass 5).
  The starter now carries it on four two-cell masts on the gunwales: 95 parts, mass 675, lift 1200, about 14.2 m/s.
  The side balloons stay available (and "New ship" uses them). Balloons now have box colliders: a scaled sphere
  collider is a ball the size of the largest axis.
- **Built-in names are reserved.** Found when the scenarios loaded a saved design called "Starter": a save under a
  built-in's name used to replace the built-in for every session. Saving under one is now refused, and a built-in
  always loads by its name. The developer's saved ship was renamed to `My Starter.ship.json` in the designs folder.

## Follow-up 2 (developer feedback, 2026-10-04)
- **Delete is a mode.** X toggles delete mode. The part under the cursor is highlighted in the blocked colour, drawn a
  little bigger than the part, and a left click deletes it. X again returns to building. The right mouse button no
  longer deletes; it orbits, like the middle button.
- **The mouse wheel zooms.** It did not before: Input System 1.19 normalizes the wheel to about 1 per notch and the
  reader clamps axes to ±1, while the zoom assumed 120 per notch. Now `ShipyardConfig.ZoomStep` is metres per notch
  (2).

## Open question: how do fixtures get onto a designed ship?
**The question.** Fuel system, cannon stations, repair rack, damage points and the voyage state reach a ship as
installer fixtures at fixed ship-local poses, made for the hand-built airship. A designed hull does not match those
poses. Today only the helm works on a designed ship: it is a part whose networked prefab is the fixture. How should
features attach to a ship built from a design, so they interface with the hull, without the builder knowing them?

**Constraint (developer):** this belongs to the integration with other systems, not to the builder. The builder places
parts and keeps the file format; features decide what goes where.

**Option A: sockets on parts (recommended).**
- *Core contract* (`Core/Ship/`): a `ShipSocketType` asset (Socket.FuelIntake, Socket.Weapon, Socket.Storage,
  Socket.DamagePoint, ...) and a `ShipSocket` marker on part prefabs: a pose and a type. Parts declare sockets in
  their prefab; the builder never reads them.
- *A built ship exposes its sockets:* `ShipHullAssembler` collects the markers of the parts it builds into an
  `IShipSockets` per ship: type, ship-local pose, part instance id. Every peer builds the same parts, so the sockets
  need no replication.
- *Integration:* a feature contributes `ShipSocketFixture` assets (socket type → fixture prefab, plus a rule: every
  socket, the first one, or up to N) through `FeatureInstaller.IExtension`. A server use case spawns them, as
  `ShipFixtureSpawningUseCase` does for fixed poses. Spawned fixtures get stable ids from (part instance id, socket
  index), so saves can keep their state. Removing a part despawns its socket fixtures.
- *Fixed-pose fixtures stay* for ships that are not designed. `IShipFixtureFilter` already keeps them off designed
  ships.
- *Examples:* an engine part with a FuelIntake socket gets the fuel system; a "gun port" part with a Weapon socket gets
  a cannon station; damage points can sit on any hull block.

**Option B: features contribute their own parts.** The fuel tank is a part whose networked prefab is the fuel
fixture, exactly like the helm. It is the simplest and already works. But the player must place every system by
hand, and a feature cannot add to existing parts (a damage point on every block).

**Option C: a second layer in the design.** The shipyard places fixtures freely (snapped to faces), and the file gets
a `fixtures` list keyed by fixture id. This gives the most player control, but the builder and the file format now
know fixtures exist, which is closer to what the constraint rules out.

**What A would take** (about two slices):
1. Core: `ShipSocketType`, `ShipSocket`, `IShipSockets`. The assembler collects sockets (tests: rotated parts give
   rotated socket poses).
2. Integration: `ShipSocketFixture` contributions and a server `ShipSocketFixtureUseCase` (spawn, despawn on part
   removal, stable ids), with tests. Optionally a validator hook, so a feature can say "needs at least one FuelIntake".
3. Per feature, asset work: socket markers on part prefabs (engine: FuelIntake; a new gun-port part: Weapon; blocks:
   DamagePoint) and each feature's socket-fixture asset.
4. A scenario: a designed ship gets its fuel system at the engine's socket on host and client, and loses it when the
   engine is removed.
5. No file format change: sockets come from part prefabs. Per-socket settings (which cannon) would later use a
   placement's extension fields, which the format already keeps.

**Decisions needed:** A, B or C; and with A, whether every matching socket gets its fixture automatically or the player
chooses per socket in the shipyard.

### Decision (developer, 2026-10-06): sockets
- **Option A, sockets on parts**, as the developer pictures it: sockets are free mount points a ship's parts provide.
  Another system (crafting, later) adds fixtures to them; any fixture fits any socket for now. Typed sockets can narrow
  that later.
- **What is mounted is live ship state**, replicated by the server. It is not part of the design file, so it is lost
  with the session until persistence exists.
- **Until crafting exists, players attach in the world:** look at an empty socket, press E, pick a fixture from a menu.

### S5: Sockets and fittings
- *Core* (`Core/Ship/Sockets/`):
  - `ShipSocket`: a marker on part prefabs, the mount pose.
  - `ShipSocketId`: the part's instance id plus the socket's index in that part.
  - `IShipSockets`: a ship's sockets, implemented by ShipDesigns from the parts it built.
  - `ShipFittingDefinition`: a fitting id, a name and a networked prefab; features contribute these through
    `FeatureInstaller.IExtension`.
  - `IModuleSpawningService` returns what it spawned and can despawn it, so mounted fixtures can be removed.
- *Integration feature* `TinCan.Features.ShipSockets` (the builder does not know it):
  - a ShipSocketsState fixture that replicates what is mounted where;
  - a server `ShipFittingUseCase` that mounts, refuses taken or vanished sockets and reaches more than 4 m away, and
    despawns a fitting when its part is removed;
  - every peer turns free sockets into interaction targets;
  - E on one asks the server, which opens the fitting menu on that player's peer only; the choice goes back to the
    server as a request it validates;
  - a fixture filter keeps the fittings' fixed-pose fixtures off ships that have sockets.
- *Parts and fittings:* a new `hull.mount` part (a deck plate with one socket on top). The starter's two bow deck tiles
  become mount plates. The cannon station is the first fitting (from the Cannon feature) and the repair rack the second
  (from Damage). Test_Shipyard loads the Cannon.
- *Scenario* `MountFitting`: the subject faces a free socket, presses E, picks the cannon. Host and client see the cannon
  mounted on that socket.

*S5 outcome (2026-10-06):*
- Built as planned. Core: `Core/Ship/Sockets/` (`ShipSocket`, `ShipSocketId`, `ShipSocketInfo`, `IShipSockets`,
  `ShipFittingDefinition`). `IModuleSpawningService.SpawnModule` now returns the spawned object, and `DespawnModule`
  removes it.
- ShipDesigns provides the sockets: `ShipAssemblyUseCase` is `IShipSockets`. Socket index i is the i-th `ShipSocket`
  under a built part.
- Feature `TinCan.Features.ShipSockets`:
  - E on a free socket reaches the server (`MountFittingInteractionHandler`), which asks that player's peer to choose
    (an RPC to one client).
  - The fitting menu is built from the loaded fittings. The choice goes back as a request the server checks: known
    fitting, a socket the ship has, not taken, within 4 m.
  - Mounts replicate as a `NetworkList` on the ShipSocketsState fixture. A fitting whose part is removed is
    despawned.
  - To avoid another dependency cycle, the menu row's command depends only on a small `ShipFittingChoice` holder,
    never on the menu system.
- Fittings: the cannon station (Cannon feature) and the tool rack (Damage). Test_Shipyard loads the Cannon; its
  fixed-pose cannon fixtures are kept off by `ShipSocketsFixtureFilter`, so cannons come only through sockets.
- Part `hull.mount`: a deck plate with a socket on top. The starter has two, on the bow row.
- **Helm footprint corrected** to 3 × 2 × 5 cells: the stand mesh reaches 2.35 m forward, past the 3-cell footprint.
  The first host + client run caught it: a player aft of a bow socket was looking across the stand.
- Scenario `MountFitting` passes solo and host + client. The client faces a socket, presses E, the menu opens on the
  client only, it picks the cannon, and both peers see the cannon on that socket. EditMode suite 922/922.
- Not built yet: removing a fitting in the world (`IShipFittings.Unmount` exists; no player action calls it), socket
  types, crafting as the source of fittings, and keeping mounts across sessions.
