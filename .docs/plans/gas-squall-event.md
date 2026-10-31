Status: Done (2026-10-31: built; EditMode and scenarios green; human playtest pending)

# Gas squall: gas pockets in the event, on the chart, and breaking what's in the sockets

Developer request (2026-10-31): fold the gas challenge into the ship socket system and the designed events, as a
fluid part of the current event. This plan replaces M5 of `main-game-state.md` and pulls in its M3 and M4, which this
needs. M0–M2 of that plan stay open and separate.

## Decisions (developer, 2026-10-31)
- **One combined event.** Gas is a phase of the event, not a separate event in the rotation.
- **The blast breaks fittings.** A detonation breaks what is mounted in the sockets near it, which the crew then repairs.
- **Pockets are on the navigation chart,** through the existing `IChartSource`, plus a new filter interface so later
  features can hide or show chart content (for example "gas only with a detector mounted").
- **Scope: M3 + M4** of `main-game-state.md` (damage points on designed ships; event state every peer sees).
- **GasChallenge leaves `Profile_Base`** (into the gameplay profiles and `Profile_Test_Core`).

## Today (verified 2026-10-31)
- `GasPocketVolume` is a scene MonoBehaviour (no NetworkObject), placed by hand in the `cvg_*` scenes.
  `GasChallengeUseCase` (server, `AfterAirship`) detonates it on first touch and applies `GE_GasPocketExplosion`
  (-350 health) to the ship. Detonation is not replicated; nothing spawns pockets.
- The only event is `HullStress` (`Features/Airship/Damage/ShipDamageDesignedEvents.cs`): linear POC phases, breaks
  points 0 and 2 by index. The director rotates the whole catalog. Announcements reach the host only.
- The Galleon has no damage points (`ShipDamageSocketsFixture` is kept off by its preset), so nothing on it can break.
- A broken part is GAS state: any effect that drops a point's health breaks it, and `ShipBreakageUseCase` reconciles
  the hull breach, the marker and the events. A blast only has to apply an effect to the right points.
- The chart reads `Core/Domain/Navigation/IChartSource` (sky islands, sky hazards). `ChartObstacleKind` is `Rock` or
  `Hazard`; `ChartPresenter` draws one pool per kind. There is no filter step.
- `IShipFittingState.Mounted` (ShipSockets) lists each mounted fitting with its socket and NetworkObject id.

## Design

### G1. Event state every peer sees (main-game-state M4, unchanged)
`EventStateNetworkMediator` on a pose-less `EventState` fixture listed by `EventsFeatureInstaller`: event id, phase
index, announcement, last outcome. `AnnounceActionHandler` writes the state; `EventHudPresenter` shows it on every
peer, late joiners included.

### G2. Damage points on designed ships, and on fittings (main-game-state M3, extended)
- Hull spots as in M3: a core `Core/Ship/IShipHullSurfaces` implemented by `ShipAssemblyUseCase`; a pure
  `ShipDamageSpotProcessor` picks `DesignedShipPoints` spots; `ShipDamagePointSpawnUseCase` (server) spawns a
  single-point prefab `Prefabs/Airship/Parts/ShipDamagePoint` per spot under the ship.
- **New: one point per mounted fitting.** A core contract `Core/Ship/Sockets/IMountedFittings`
  (`FittingsOn(shipId)` → socket, fitting id, root transform), implemented by ShipSockets from `IShipFittingState`,
  so ship damage does not reference ShipSockets. The spawner keeps one point beside each fitting (at its socket's mount,
  raised to reach height) and despawns it when the fitting is unmounted. A point knows whether it belongs to a
  fitting (`IShipDamagePoint.Socket`, null for hull spots), so the HUD and logs can say "the starboard cannon broke".
- Indexes: hull spots 0..N-1, then fittings in socket order. `HullStress` keeps breaking 0 and 2, both hull spots.
- **A broken fitting still works** (settled below). It is a repair job and a hull breach (the leak, once M2 gives
  the Galleon a tank), with the marker on the fitting.

### G3. Networked pockets and the gas field
- `Prefabs/Hazards/GasPocket` becomes networked: NetworkObject, `EntityNetworkMediator`, and `GasPocketNetworkMediator`,
  which replicates `Detonated`, plays `Cue.Gas.Detonate` (`GCN_Gas_Detonate`, listed on the installer) and hides the
  visual. `GasPocketVolume` and the touch rule are unchanged, so the hand-placed pockets in `cvg_*` keep working.
- `GasFieldProcessor` (pure): N spots ahead of the bow along the ship's motion, spread across and above, skipping
  spots an `IWorldObstacleQuery` blocks. `GasPocketSpawningService` (server) spawns and despawns them, as
  `SkyHazardSpawningService` does.

### G4. The blast breaks what's near it
- On detonation `GasChallengeUseCase` still applies the explosion effect to the ship, then `GasBlastProcessor`
  (pure) picks the damage points within `BlastRadius` of the contact point (closest point on the ship's collider to
  the pocket's centre): fittings first, then hull spots, nearest first, at most `MaxBreaks`. Each gets the ship damage
  config's break effect; `ShipBreakageUseCase` does the rest.
- Tuning on `GasChallengeConfig` (new; today the installer has no config): `BlastRadius` 8 m, `MaxBreaks` 2. Lower
  `GE_GasPocketExplosion` from -350 so the breaks, not the health hit, are the cost; checked against the Galleon's hull
  in `ShipStatsProcessor`.

### G5. The combined event
GasChallenge contributes it and references DesignedEvents and Airship.Damage (for `BrokenPartsAtMost`):
`GasSquall` (id 2, `Features/GasChallenge/GasChallengeDesignedEvents.cs`):
1. **Warn** (4 s): "Gas ahead: steer clear!"; `SpawnGasField(count 5, ahead 150 m, spread 40 m)`.
2. **Field**: until `GasFieldCleared` (each pocket of this field detonated or more than `PassedDistance` behind the
   ship); timeout 60 s.
3. **Repair**: "Check the ship and repair the breaks!"; until `BrokenPartsAtMost(0)`; timeout 90 s. A crew that steered
   clean passes at once.
4. Success "Clear of the gas, the ship holds." Failure "The hull gave way." Both run `ClearGasField`.

**One event, not two:** `EventDefinition` gains `Supersedes(id)`. Auto-rotation skips an event another loaded event
supersedes, so with GasChallenge loaded the director runs `GasSquall` instead of `HullStress`; without it (in
`Profile_Test_ShipDamage`), `HullStress` runs as today. `TryStart` by id still starts either, so `HullStressEvent` stays
green.

### G6. Gas on the chart, and chart filters
- `ChartObstacleKind.Gas` (core). `ChartPresenter` gets a gas pool and `NavigationConfig.GasMaterial`.
- `GasPocketChartSource` (GasChallenge, every peer) adds every spawned, undetonated pocket as a gas obstacle. It reads
  the replicated pockets, so every peer charts the same. Hand-placed pockets are left off (not replicated).
- **Filters:** a core `Core/Domain/Navigation/IChartFilter` (`ShowsObstacle(in ChartObstacle)`,
  `ShowsPoint(in PointOfInterest)`), registered with `.As<IChartFilter>()`. `ChartContents` drops whatever any filter
  hides; with none registered, everything shows. No filter ships now; the interface is tested with a fake one.

## Assemblies and profiles
- GasChallenge gains references: `TinCan.Features.DesignedEvents`, `TinCan.Features.Airship.Damage`, `TinCan.Entities`,
  NGO, `TinCan.Core.Domain` (navigation, obstacles), `TinCan.Gas` cues.
- GasChallenge leaves `Profile_Base`; it goes into `Profile_Galleon`, `Profile_FuelSandbox` and `Profile_Test_Core`
  together with Events and ShipDamage (the profile rule test enforces this). `cvg_*` scenes keep it through their
  profile; checked per scene while building.
- ShipDamage reads only core contracts (`IShipHullSurfaces`, `IMountedFittings`); no new feature reference.

## Order
Each slice ends green on `.\.tools\verify.ps1 -UpTo Tests`; one confirmed play run at the end.
1. G1 event state. 2. G2 damage points (hull, then fittings). 3. G3 networked pockets and field. 4. G4 blast.
5. G5 event and `Supersedes`. 6. G6 chart and filters. 7. Profiles, docs, scenarios.

## Tests
- EditMode: `EventHudPresenterTests`, `EventDirectorUseCaseTests` (state, `Supersedes`), `ShipDamageSpotProcessorTests`,
  `ShipDamagePointSpawnUseCaseTests` (hull spots, a point per fitting, unmount despawns), `GasFieldProcessorTests`,
  `GasBlastProcessorTests`, `GasFieldActionHandlerTests`, `GasFieldClearedConditionHandlerTests`,
  `GasPocketChartSourceTests`, `ChartContentsTests` (filters), a catalog case that `GasSquall` builds and its handlers
  resolve in `Profile_Galleon`.
- Scenarios (`cheesed_scene` / `Profile_Galleon` until main-game-state M1 builds `scene_main`), solo and host + client:
  `GasSquallEvent` (start it; the pockets show on both peers and on the chart; fly into one; it detonates on both peers;
  the cannon nearest it breaks; repair it; the event succeeds; the client saw the announcements). `HullStressEvent`,
  `GalleonLoadout`, `GalleonChartTable` stay green.
- Human playtest: pocket readability, blast radius against the deck layout, whether dodging feels possible.

## Docs
`CODE_MAP.md` (Gas pocket challenge, Ship damage, Designed events, Navigation rows; GasChallenge stops being the
"no Netcode" worked example, so the assemblies section points at another), `designed-events.md` (E1b partly built,
`Supersedes`), `main-game-state.md` (M3, M4, M5 point here).

## Settled (developer, 2026-10-31)
1. **A broken fitting keeps working:** a repair job and a hull breach only. Making it stop working is a later follow-up.
2. **`Supersedes`** on `EventDefinition`, not a rotation list on the director settings.

## Outcome (2026-10-31, built and verified)
All six slices are in code, with assets from **TinCan > Dev > Gas Squall > Build Assets**
(`DevTools/Editor/GasSquallAssetBuilder.cs`). EditMode 1218/1218. Scenarios solo and host + client: `GasSquallEvent`,
`HullStressEvent`, `ShipDamage`, `RepairLoop`, `GalleonLoadout`, `GalleonChartTable` all pass. Changes from the design above:
- **The burst is not a gameplay cue.** A pocket has no GAS controller to carry one, so `GasPocketNetworkMediator` hides
  the cloud and plays a "Burst" particle child, when the prefab has one (it has none yet: an art task).
- **Blast reach is measured from the pocket's centre** (its radius plus `BlastRadius`), not from a contact point:
  `PhysicsGasPocketQuery` takes the ship's first collider, which on a ship built from parts can be any part.
- **The field is wider:** the pocket prefab's radius is 16 m, so `GasSquall` lays 5 pockets 200 m ahead across
  +-150 m (60 m lanes), not 5 across +-40 m, which would be a wall.
- **The networked pocket is a variant,** `Prefabs/Hazards/GasPocket_Networked`, so the hand-placed pockets in the `cvg_*`
  scenes stay plain scene objects.
- **Profiles:** GasChallenge left `Profile_Base` and `Profile_Test_Core` and went into `Profile_FuelSandbox` (so
  `Profile_Galleon` and `cheesed_scene`) and `Profile_Test_ShipDamage`. A scene with no profile loads every installer,
  so `cvg_gaspocket_test` is unchanged.
- **`GE_GasPocketExplosion`** is -150 health (was -350): the breaks are now the main cost. Needs a playtest.
- **The scenario** `GasSquallEvent` runs in `cheesed_scene` (no `scene_main` yet). It moves one pocket onto the ship
  (the real touch rule sets it off), checks the breaks beside fittings and the chart on the subject, then disperses the
  rest of the field (`DisperseGasField`), restores the breaks and expects success.
