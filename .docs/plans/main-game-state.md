Status: Draft

# The main game: scene_main, Profile_Main, and the Galleon's fuel, hull stress and gas pockets

## Goal
One scene and one profile are "the current state of the game": `scene_main` running `Profile_Main`, flying the
Galleon. On that ship the fuel loop works through the socket system, the hull can be stressed and broken (and
repaired), and gas pockets arrive as a designed event every peer sees. The stern cannons stop throwing their gunner
off the quarterdeck.

## Today (verified 2026-10-18)
- **The live game** is `cheesed_scene` with `Profile_Galleon` (= `Profile_FuelSandbox` + ShipDesigns + ShipSockets).
  `Profile_FuelSandbox` also drives the old-airship scenes `drm_cloud_environment` (build index 0) and
  `cvg_airship_default`. Six Editor builders add their installer to `Profile_FuelSandbox` as the "main profile"
  (`DevTools/Editor/{Cannon,Helm,GrappleHook,Respawn,Navigation,SkyIslands,Voyage}AssetBuilder.cs`), and
  `ShipSocketsAssetBuilder` wires `cheesed_scene`. Scenarios `GalleonLeashClimb` and `GalleonChartTable` run in
  `cheesed_scene`.
- **No fuel on the Galleon.** `Galleon_Prefab`'s `ShipDesignPreset` keeps the old-hull fixtures off, `FuelSystemFixture`
  among them. `FuelSystem.prefab` is one NetworkObject holding the tank, the crystal rack (`JerryCanSupply`, z 21), the
  receiver (`MotorFillPort`, z -11.5) and the gauge (z -8.4), so it can't go into one socket. The tank is found under the ship
  (`FuelTankLocator`, `GetComponentInChildren`); the fill port finds it with `GetComponentInParent`.
- **No damage points on the Galleon.** `ShipDamageSocketsFixture` is kept off the same way. `HullStress`
  (`Features/Airship/Damage/ShipDamageDesignedEvents.cs`) breaks points 0 and 2 by index (`ShipDamageLocator.FindPoints`:
  every `IShipDamagePoint` under the ship, ordered by `Index`), so on the Galleon it does nothing. The same goes for
  random breakage, repair and the hull-breach leak.
- **Gas pockets** are plain scene objects (`GasPocketVolume`, no NetworkObject), placed by hand in the `cvg_*` scenes
  only. `GasChallengeUseCase` (server, `AfterAirship`) detonates one on first touch and applies `GE_GasPocketExplosion`
  (-350 health) to the ship. Their detonated state isn't replicated.
- **Designed events** are the POC (`plans/designed-events.md`, parked): one event at a time, the director rotates the
  catalog, and the HUD announcement shows on the host only.
- **Fittings work under the ship.** `ShipFittingUseCase` spawns a fitting through `IModuleSpawningService` parented to
  the ship, so locators that search under the ship find it (the tool rack already proves it).

## Bug: the stern cannons drop their gunner off the quarterdeck
**Likely cause (from the data; not yet seen in play).** `CannonStation.prefab`'s `Seat` is at local (0, 1.05, -1.8),
1.8 m behind the cannon's base, and occupying teleports the player there (`StationOccupancyUseCase.TryOccupy`,
`ResetCharacter`). The Galleon's stern cannons sit on the quarterdeck, the helm's deck, on mount plates at z -7.5 with
yaw 0 (`ShipFrameDesigns.Galleon`, `SternPort`/`SternStarboard`). The quarterdeck ends at z -8.5, with a rail, so the
seat lands at z -9.3, outside the stern rail, and the player falls. The bow cannons' seats (z 4.7) land on the side
deck, which matches the report: only the cannons by the helm do it.

**Fix**
1. A scenario that reproduces it first, red: `GalleonSternCannon` (scene_main): take a stern cannon and check that the
   player stays aboard for 3 s, solo and host + client.
2. Data: turn the two stern plates to face aft (`OnFace(..., yaw 2)`, as stern chasers). Their seats move to z -5.7,
   clear of the helm at x 0. Regenerate `Galleon.ship.json` from `ShipFrameDesigns`; the part ids stay, so
   `Galleon.fittings.json` is unchanged.
3. Guard, because players can mount a cannon by hand on any free socket (the balcony would do the same thing):
   `ShipFittingUseCase` refuses a station fitting whose seat isn't over the ship. Check it with a short ray down from
   the seat against the ship's own colliders, through a small `IStationSeatProbe`, so the rule stays testable. The
   refusal message reaches the player the same way the other refusals do.
4. Tests: a `GalleonLoadoutTests` case that every station seat in the built-in loadout sits on a floor face of the
   frame; a `ShipFittingUseCaseTests` case for the refusal.

## Slices
In this order. Each one ends green on `-UpTo Tests`, and the play runs are batched into one confirmed run per slice.

### M0. The bug (above)
Small and independent, so it goes first; its scenario lands with M1's scene.

### M1. scene_main and Profile_Main
- `Assets/Settings/FeatureProfiles/Profile_Main.asset`: one flat list, with no include of `Profile_FuelSandbox`: the base
  features, every feature the sandbox loads today, plus ShipDesigns and ShipSockets (later slices add theirs). The main
  game then no longer depends on the legacy profile.
- `Assets/Scenes/scene_main.unity`, new: `GameLifetimeScope` with `Profile_Main`, `Galleon_Prefab` as the ship, and the
  cloud environment and lighting copied from `cheesed_scene`. It's built by a menu item, **TinCan > Dev > Main > Build
  Scene** (`DevTools/Editor/MainSceneBuilder.cs`), so it can be rebuilt instead of edited by hand. `cheesed_scene`
  stays as the developer's scene, untouched.
- Build settings: `scene_main` at index 0.
- The builders' "main profile" constant becomes `Profile_Main` (one shared constant, `MainGame.ProfilePath`, in
  DevTools/Editor). `Profile_FuelSandbox` and `Profile_Galleon` are marked legacy in a comment and in `CODE_MAP.md`.
- Scenarios `GalleonLoadout`, `GalleonLeashClimb` and `GalleonChartTable` move to `scene_main`.
- Rule test: `MainGame_ProfileLoadsEveryGameplayFeature`, so that `Profile_Main` holds every installer that
  `Profile_FuelSandbox` holds, which catches a builder that still writes to the old profile.
- Docs: `CODE_MAP.md` (scenes row, profile notes), `FEATURE_INSTALLERS.md` (the main scene), `ARCHITECTURE.md`
  (cheesed_scene mention), `.docs/README.md`.

### M2. Fuel through the sockets
- **The tank is ship state.** It moves out of `FuelSystem.prefab` into a new pose-less `FuelTankState` fixture
  (`Prefabs/Airship/Parts/FuelTankState`, `Settings/Fixtures/FuelTankStateFixture`), spawned on every ship like
  `VoyageState`. So every ship drains and stalls, with or without fittings.
- **The fittings** (each a NetworkObject + `EntityNetworkMediator`, its pivot at its base):
  - `fuel.receiver` (`FITTING_FuelReceiver`): `MotorFillPortNetworkMediator`, `FuelMotorStatusView`, `FuelGaugeView`.
  - `fuel.crystal_rack` (`FITTING_CrystalRack`): `JerryCanSupplyNetworkMediator` and its three cans.
  - `fuel.net_rack` (`FITTING_NetRack`, contributed by the Minigame installer): the existing net rack.
- **Lookups move to the ship.** The fill port finds the tank with `FuelTankLocator.Find(ship)` instead of
  `GetComponentInParent`; the gauge already falls back to the ship. With several crystal racks, net catches and
  `FuelConsumptionUseCase` use the first one found, as today; the plan leaves that as a known limit.
- **The old airship keeps working.** `FuelSystem.prefab` keeps its rack, receiver and gauge but loses the tank, and
  `FuelTankState` gives it one. `ShipSocketsFixtureFilter` already keeps a fixture off when its prefab is a fitting;
  the Galleon preset still keeps `FuelSystemFixture` off.
- **The Galleon's loadout** mounts one of each on its three free plates: the receiver in the hold, port aft (near
  the cairn); the crystal rack in the hold, starboard aft; the net rack on the cabin floor (the main deck, open to
  the sky for catching). If the layout plays badly, add plates to the design instead (new placement ids,
  existing ones unchanged).
- **Tuning note:** the Galleon starts draining, so voyage length versus fuel needs a playtest (`FuelConfig`).
- Tests: `FuelFixtureRegistrationTests` and `FuelConsumptionUseCaseTests` for a tank with no receiver;
  `GalleonLoadoutTests` for the three fuel fittings; `MotorFillPort` finding the tank through the ship.
- Scenario `GalleonFuelLoop` (scene_main): the tank drains while flying, a crystal from the rack is poured into the
  receiver, the level rises on both peers. `NetCatch` and the `FuelConsumption*` scenarios stay green on the old
  airship.

### M3. Damage points on designed ships (hull stress on the Galleon)
- **Where the points come from: the frame.** A built designed ship offers its hull surfaces through a core contract
  `Core/Ship/IShipHullSurfaces` (ship-local plank floors and hull walls, with normal and story), implemented by
  `ShipAssemblyUseCase` next to `IShipSockets`. Ship damage doesn't reference ShipDesigns, so `Profile_Test_ShipDamage`
  still works without it.
- **Choosing spots:** `ShipDamageSpotProcessor` (pure) picks `ShipDamageConfig.DesignedShipPoints` (default 5) spots
  spread across stories and faces, the same for a given design (seeded by the design hash). Every spot must be
  reachable by `TD_RepairScan` from a floor; a test checks this for the Galleon.
- **Spawning:** `ShipDamagePointSpawnUseCase` (server): once a designed ship stands built and has no fixed damage
  points, it spawns a new single-point prefab `Prefabs/Airship/Parts/ShipDamagePoint` (one NetworkObject per point,
  made from one `ShipDamageSockets` socket: marker, collider, attribute bar, cue handler) per spot with `Index` 0..N-1.
  When the ship is rebuilt, its points despawn. Old ships keep the fixed `ShipDamageSockets` fixture.
- Then `HullStress`, random breakage, the repair tool, the breach fuel leak (which needs M2's tank) and the "Hull" HUD
  meter all work on the Galleon without further changes.
- Tests: `ShipDamageSpotProcessorTests`; `ShipDamagePointSpawnUseCaseTests` (spawn once, skip ships with fixed
  points, despawn on rebuild); a surfaces case in `ShipAssemblyUseCaseTests`.
- Scenario `GalleonHullStress` (scene_main): start `HullStress`; points 0 and 2 break on both peers; the crew repairs
  them with the tool from the rack; the event succeeds. `HullStressEvent`, `ShipDamage`, `RepairLoop` and
  `ShipDamageLateJoin` stay green.

### M4. Event state every peer sees (minimal E1b)
- `EventStateNetworkMediator` on a pose-less `EventState` ship fixture (listed by `EventsFeatureInstaller`, the same
  pattern as `VoyageState`): it replicates the event id, the phase index, the current announcement and the last outcome.
  The director writes it on the server.
- `AnnounceActionHandler` stops writing `IHudValues` and writes the announcement into the state;
  `EventHudPresenter` (every peer) shows it. Late joiners read `.Value` in `OnNetworkSpawn`.
- This doesn't cover E1a (recipes), E1c (variation) or E1d (schedule); the director still rotates the catalog.
  `designed-events.md` records that E1b is partly built.
- Tests: `EventHudPresenterTests`, and `EventDirectorUseCaseTests` writing the state. `HullStressEvent` gains a client
  probe for the announcement.

### M5. Gas pockets as a designed event
- **Networked pockets.** `Prefabs/Hazards/GasPocket` becomes networked: NetworkObject, `EntityNetworkMediator`, and a new
  `GasPocketNetworkMediator` that replicates `Detonated`, plays the burst cue `Cue.Gas.Detonate` (a `GCN_Gas_Detonate`
  notify listed on the installer) and hides the visual. `GasPocketVolume` and `GasChallengeUseCase` stay as they are,
  so the scene-placed pockets in the `cvg_*` scenes still work; for a networked pocket, the server's detonation sets
  the replicated flag.
- **Spawning:** `GasPocketSpawningService` (`IGasPocketSpawner`, server, world objects like `SkyHazardSpawningService`)
  and `GasFieldProcessor` (pure): N spots ahead of the bow along the ship's motion, spread across and above, skipping
  spots an `IWorldObstacleQuery` blocks (island rock).
- **Event vocabulary** (GasChallenge then references DesignedEvents, as ship damage does): actions
  `SpawnGasField(count, ahead, spread)` and `ClearGasField`; condition `GasFieldCleared` (every pocket from this
  event has detonated or is more than `PassedDistance` behind the ship). Handlers are registered in
  `GasChallengeFeatureInstaller`.
- **The event** `GasField` (id 2, `Features/GasChallenge/GasChallengeDesignedEvents.cs`): "Warn" (announce "Gas
  ahead: steer clear!", 4 s), then "Field" (spawn 5 pockets 150 m ahead; until cleared; 60 s timeout), then success
  "Clear of the gas", or on failure, "Lost in the gas", plus a clear-up. The director rotates `HullStress` and
  `GasField`.
- **Tuning:** `GE_GasPocketExplosion` is -350 health. Check that against the Galleon's hull (`ShipStatsProcessor`) so
  one pocket doesn't sink the ship.
- Tests: `GasFieldProcessorTests`, `GasFieldActionHandlerTests`, `GasFieldClearedConditionHandlerTests`; a catalog
  case that `GasField` builds and its handlers resolve in `Profile_Main`.
- Scenario `GasFieldEvent` (scene_main): start `GasField`; the pockets appear on both peers; one is flown into, it
  detonates on both peers and the ship loses health; the event ends, the announcement shows on the client.
  `HullStressEvent` stays green.

## Assemblies
- GasChallenge gains references to `TinCan.Features.DesignedEvents`, `TinCan.Entities`, NGO and `TinCan.Core.Domain`
  (navigation and obstacles). `Profile_Main` and every profile that loads GasChallenge (`Profile_Base`!) then need
  DesignedEvents, because `FeatureProfiles_LoadTheFeaturesTheirFeaturesReference` enforces it. **Decision needed
  before M5:** move GasChallenge out of `Profile_Base` (into `Profile_Main` and `Profile_Test_Core`), or split the event
  vocabulary into a small `GasChallenge.Events` assembly. Recommended: the move; `Profile_Base` keeps only what
  every scene needs.
- ShipDamage needs no new feature reference (M3 uses the core contract).

## Docs
Feature index rows and notes in `CODE_MAP.md` (Fuel, Ship damage, Designed events, Gas pocket challenge, Ship sockets);
`designed-events.md` (GasField, partial E1b); `ship-fitting-loadouts.md` (fuel fittings); the scene and profile notes
under M1.

## Verification
- Every slice: `.\.tools\verify.ps1 -UpTo Tests`.
- Play runs (asked for each time, batched): M0+M1 `GalleonSternCannon`, `GalleonLoadout`, `GalleonLeashClimb`,
  `GalleonChartTable`; M2 `GalleonFuelLoop`, `NetCatch`; M3 `GalleonHullStress`, `HullStressEvent`, `RepairLoop`,
  `ShipDamageLateJoin`; M4+M5 `GasFieldEvent`, `HullStressEvent`. Solo and host + client.
- Human playtest: fuel pacing on a voyage, where the damage points land and how reachable they are, gas pocket
  readability and damage, the stern-chaser cannons' arcs.

## Decisions
Taken with the developer (2026-10-18):
- A new `scene_main` and `Profile_Main`; `cheesed_scene` stays as the developer's scene.
- Fuel: the tank is ship state; the receiver, crystal rack and net rack are fittings.
- Hull stress means damage points on designed ships, so the existing HullStress event works on the Galleon.
- Gas pockets come with a minimal replicated event state, so clients see the announcements.

Open:
- GasChallenge's place in `Profile_Base` (see Assemblies).
- The stern cannons as stern chasers (recommended) or moved forward on the quarterdeck.
- `Profile_Main` flat (recommended, this plan) versus including `Profile_Base`.
