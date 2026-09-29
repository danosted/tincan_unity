Status: Approved

# Cannon + sky hazards

## Progress (2026-09-28)
S1 is built and playable in the sandbox scenes (`Profile_FuelSandbox`) and in `Test_Cannon` (scenario `CannonShot`).
Where the build differs from the design below, and why:
- **Occupying goes through a handler, not `ActivateAbilityInteractionHandler`.** `IA_OccupyCannon` points at
  `OccupyStationInteractionHandler`. It calls `StationOccupancyUseCase`, which activates `GA_OccupyCannon` on the
  player. The use case has to remember which station a player occupies, and an ability spec does not keep its target.
- **Leaving is "Interact again", not Cancel.** Interact is already a server-read input bit. Cancel belongs to the menu
  bootstrap (`UI_FRAMEWORK.md`). The hook is `IInteractOverride` in `InteractInputUseCase`.
- **Walking is locked by attributes, not by movement code.** `GE_Occupying_Cannon` overrides MoveSpeed and JumpForce to
  0. Humanoid movement already reads them, and they replicate to the owner.
- **Firing is read on the server only.** `CannonFireUseCase` sees the occupant's Primary press and calls
  `TryActivateAbility(GA_FireCannon)`, whose cooldown is the reload. `GA_FireCannon` has no TriggerInput, so the
  owner does not predict it. Owner-predicted fire and the muzzle flash stay in S3.
- **Camera (2026-09-29, developer: "you can't see anything"):** the gunner looks through a camera over the barrel
  (`PitchPivot/CameraMount`, turning and tilting with the aim). `StationViewPresenter` (Stations) swaps it in for the
  local occupant and swaps the body camera back on leaving. `PossessedViewCamera` asks its `ILocalViewOverride` first.
  This is still occupancy, not possession: only the view changes, and the body's predicted look keeps aiming. A
  LineRenderer arc (`AimPreview`) shows where a shot will fly.
- **Aim follows the look, not the body (2026-09-29):** the body turns only in simulation ticks, so a barrel driven by
  it stepped visibly under the barrel camera. The local gunner's barrel follows the per-frame look
  (`IHumanoidMovementView.LookRotation`/`LookPitch`). The server aims from that tick's input look (platform yaw ×
  `InputState.LookRotation`). The inherited velocity is the ship's motion at the muzzle (`BaseVelocityAt`), without
  the barrel's swing.
- **Cosmetic balls are timed from when a peer hears of the shot.** Ticks are not comparable across peers, so
  `ShotFired` carries origin and velocity only.
- **Cues are deferred.** There is no fire, impact or destroyed cue yet.
- **Placement:** one starboard broadside cannon on the mid deck (y -3.49 on both the airship and the test ship), at
  z 4. That is forward of the net rack (z 0); z 1 crowded the rack and z -3 was inside the StairsTop staircase. The
  hazard field is a box on the starboard side (`SkyHazardConfig.FieldMin/Max`). `FieldEnabled` is on: S1 hazards
  only hang in the sky, so they are harmless targets. Turn the field off before S2 makes them hit the ship.
- **Tuning:** one hit destroys a hazard (`GE_CannonballHit` -100, hazard health 100). The reload is 1.5 s.
- **Assets are code:** **TinCan > Dev > Cannon > Build Assets** (`DevTools/Editor/CannonAssetBuilder.cs`) builds or
  re-tunes all of them. It also fixes each prefab's NetworkObject id, which is otherwise shared by prefabs built
  from scene objects.
- **Found on the way:** `TestRangeSceneBuilder` still set `_doorInteractionTag`, which slice 5 moved to the AirshipDoor
  installer, so Rebuild Scenes threw. The line is removed.

## Context
The developer chose between cannons, floating islands, and enemies. Cannons won because most of the groundwork exists:
- GAS health/damage (`HealthAttributeSet`, `AbilitySystemUseCase.ApplyEffect`)
- targeting (`ITargetingService`)
- ship sockets (`IShipBreakage`)
- streamed world objects (`FlyingCanSpawningService`)
- stable entity ids (`EntityNetworkMediator`)

**Sky hazards** drift at the ship and break a damage socket unless they are shot down, so shooting matters on day one. Their motion sits behind a seam, so they become the first enemies later.

This plan covers E2 (targets) and E3 (gunner) of `.docs/plans/designed-events.md` without depending on the parked E1 work. It was merged with a second session's weapons design (`in-a-unity-networked-dazzling-cascade.md`). On implementation, it is copied to `.docs/plans/cannon-and-hazards.md`. Work stays on main, and nothing is committed without the developer's consent.

## Decisions (taken with the developer, 2026-09-28)
- **Manning a cannon is occupancy through GAS, not possession.**
  - `IA_ManCannon` → the existing `ActivateAbilityInteractionHandler` (activator is the requester, target is the station) → `GA_OccupyStation` on the player.
  - While it is active, the player carries `State.Occupying.Cannon`, which locks walking and grants `GA_FireCannon`. The station carries `State.Occupied`, and `ActivationBlockedTagsOnTarget` makes it exclusive.
  - Cancel ends the ability, and the tags and grants go with it.
  - The body stays the input source: look yaw/pitch aims the barrel (clamped to the cannon's limits), and `Input_Primary` fires. This keeps the exact, owner-predicted per-tick input stream and follows ARCHITECTURE §3 ("Avoid side-channels").
  - **Possession stays** for real control transfer (NGO ownership, driving another object). A helm move to occupancy is a later, separate plan.
- **Shot model (the developer's idea):** no networked projectile.
  - The server keeps the arc analytically: `p(t)=p0+v0·t+½g·t²`, where v0 = muzzle velocity + the ship's point velocity at the fire tick.
  - Each tick it sweeps only the segment p(t_prev)→p(t_now).
  - Clients get one reliable `ShotFired(id, origin, v0, fireTick)` and draw the ball from the same formula, blended from their local muzzle onto the server arc over ~0.2 s.
  - Gizmos draw the predicted arc.
- **The sweep goes through targeting** (ARCHITECTURE §6). Add `ITargetingService.TryAcquireSegment(from, to, definition, ignoreRoot)`, which reuses `TargetingUseCase`'s hit walk and tag filters, with a `TD_CannonballSweep` (radius = ball). No friendly fire: humanoids are filtered out by tag, and the own ship through `ignoreRoot`.
- **Firing** is `GA_FireCannon`, marked with a new **`EndsImmediately`** flag on `AbilityDefinition`. Reload is the cooldown `GE_CannonReload`, which grants `State.Cannon.Reloading`. As in `NetCatchUseCase`, the ability sets intent and a server use case performs the world effect.
- **One cannon now, placement data-driven.** The station is a `ShipFixtureDefinition` fixture spawned by the installer; no `Airship_Prefab` edits.
- **Hazard streaming** is a config toggle, **off by default**.
- **Folders:** `Features/Weapons/Cannon/` (leaving room for `Weapons/Rifle/` later) and `Features/SkyHazards/`.

## Verified constraints
- `ActivateAbilityInteractionHandler` (`Features/Interaction/InteractionHandlers.cs:33`) already activates an ability on the requester against a target controller.
- `ActivationBlockedTagsOnTarget` and `ActivationBlockedTagsOnActor` are implemented (`AbilitySystemUseCase.cs:293-302`).
- `CancelAbilitiesWithTag` and `BlockAbilitiesWithTag` (`AbilityDefinition.cs:27-28`) are declared but **not implemented**. S1 implements them (this was E3's GAS cleanup).
- No movement suppression by tag exists. S1 adds it to humanoid movement: while `State.Occupying` is held, locomotion and jump are ignored and look is still read. It runs on the owner and the server alike, so prediction agrees.
- `TargetingUseCase.GatherRayHits` casts from a targeter pose with a fixed `Range`, and a solid collider that is not a target stops the ray. It cannot sweep an arbitrary segment yet, hence `TryAcquireSegment`.
- Ability grants are not replicated. The station grants follow `EquipmentAbilityBinder`'s pattern: granted on the server and on the owner, never on proxies, and it revokes exactly what it granted.
- Gameplay cues carry only a tag and a target, so `ShotFired` / `ShotEnded` are RPCs on the station mediator. Cues are used for the muzzle flash, impacts, and hazard destruction.

## S1: Occupy the cannon, fire a ballistic shot at an on-demand static target
**GAS**
- `EndsImmediately` on `AbilityDefinition`.
- Implement `CancelAbilitiesWithTag` / `BlockAbilitiesWithTag` in `AbilitySystemUseCase`.
- Tests for each.

**Occupancy (the generic station pattern; the helm may reuse it later)**
- `GA_OccupyStation`: blocked on target by `State.Occupied`, blocked on actor by `State.Occupying`.
- Its effect `GE_Occupying_Cannon` is Infinite and grants `State.Occupying.Cannon` to the player.
- A `StationOccupancyUseCase` (server) handles occupying:
  - snaps the body to the seat (platform-local pose, `TeleportEpoch`)
  - applies `State.Occupied` to the station
  - writes the occupant into the station mediator
  - grants and revokes the station's abilities in the binder style
  - ends occupancy on Cancel, disconnect, or station despawn
- Humanoid movement: locomotion is ignored while the player has `State.Occupying`.

**Targeting:** `TryAcquireSegment` with tests (a segment hit, own-root ignored, humanoid tag filtered).

**`Features/Weapons/Cannon/`**
- **Pure logic, EditMode-tested:**
  - `BallisticArc` (`FromMuzzle`, `PositionAt`, `VelocityAt`)
  - `CannonballProcessor`: tick segment; expiry on lifetime, range, or cloud height
  - `CannonAimProcessor`: occupant yaw/pitch → clamped barrel angles → muzzle direction
- **`CannonNetworkMediator`:** station entity. It holds the occupant, a replicated barrel yaw/pitch (for proxies) and the station's ability controller. It binds to the ship in `OnNetworkSpawn` / `OnNetworkObjectParentChanged`, and sends `ShotFiredClientRpc` / `ShotEndedClientRpc` to an `ICannonShotFeed`.
- **Use cases:**
  - `CannonFireUseCase` (server, `AfterHumanoid`):
    1. When `GA_FireCannon` activates at the input tick, read the occupant's aim.
    2. Build the arc and send the fire cue and `ShotFired`.
    3. For each shot in flight, `TryAcquireSegment`. On a hit, apply `GE_CannonballHit` (optional splash through an overlap), then send the impact cue and `ShotEnded`.
  - `CannonShotPresenter` (all peers): pooled cosmetic balls with the muzzle blend.
- **Views:**
  - `CannonBarrelView`: follows the occupant's aim on the owner at once, and the replicated angles on proxies. Draws the arc in `OnDrawGizmos`.
  - `CannonCameraView`: a local camera mount, active while the local player has `State.Occupying.Cannon` and this station is the one they occupy.
- **Config and installer:**
  - `CannonConfig`: MuzzleSpeed, Gravity, BallRadius, SplashRadius (0), MaxLifetime, MaxRange, YawLimit, Min/MaxPitch, Damage/HitEffect, Sweep definition, BallPrefab, PoolSize.
  - `CannonFeatureInstaller`: fixture, cues, and handler registrations.

**`Features/SkyHazards/`** (on-demand spawning only)
- `ISkyHazard`, `ISkyHazardSpawner` (the future events `SpawnTargets` backend).
- `SkyHazardSpawningService`, modeled on `FlyingCanSpawningService`.
- `SkyHazardNetworkMediator`: `ITargetable`, with a `HealthAttributeSet` and an `EntityNetworkMediator` on the root.
- `ISkyHazardMotion` (the enemy seam) and `StationaryMotion`.
- `SkyHazardUseCase` (server, `AfterAirship`): moves hazards; at 0 health, plays the destroyed cue, then despawns after a short delay.
- `SkyHazardConfig`, `SkyHazardsFeatureInstaller`.

**Assets** (through the unity MCP, not YAML)
- `CannonStation.prefab` uses the `Cannon_Module` mesh and carries `NetworkObject`, `EntityNetworkMediator`, `AbilityNetworkMediator` and `CannonNetworkMediator`.
- `SkyHazard.prefab`.
- The fixture, configs, `IA_ManCannon`, `TD_CannonballSweep`.
- `GA_OccupyStation`, `GE_Occupying_Cannon`, `GA_FireCannon`, `GE_CannonReload`, `GE_CannonballHit`.
- Tags (`State.Occupying.Cannon`, `State.Occupied`, `State.Cannon.Reloading`, `Cue.Cannon.Fire`, `Cue.Cannon.Impact`, `Cue.Hazard.Destroyed`) and the matching `GCN_*` assets.

**Tests:** `BallisticArcTests`, `CannonballProcessorTests` (position at t, sweep catches a thin wall, inherits ship velocity), `CannonAimProcessorTests`, `CannonFireUseCaseTests` (fires only while occupying, cooldown blocks, a hit applies the effect once, expiry), `StationOccupancyUseCaseTests` (exclusive, Cancel releases, a disconnect releases), `TargetingSegmentTests`, the GAS flag tests, `SkyHazardUseCaseTests`, and a handler test for `IA_ManCannon`.

**Scenario `CannonShot`** in a new area `Test_Cannon`:
- Add a `TestScenes.Cannon` constant and a row in `DevTools/Editor/TestRangeSceneBuilder.cs`.
- `Profile_Test_Cannon` = Test_Core + Fuel + ShipDamage + Cannon + SkyHazards.
- Steps:
  1. Hold Interact at the seat, then check `HasTag State.Occupying.Cannon`.
  2. `SpawnTargetOnArc 40`.
  3. Hold primary.
  4. Check `HazardDestroyed`, `CueCount Cue.Cannon.Fire:Execute:1`, and on the client `CosmeticShotsSeen ≥1`.
  5. Cancel, then check that the tag is gone and the subject can walk.
- Run solo and host + client, with the latency preset.

## S2: Streamed hazards that damage the ship
**Built differently (2026-09-29, developer decision; `first-voyage.md` V1).** A hazard that reaches the ship lowers the
ship's health (`GE_HazardImpact`, −50 on the airship's `HealthAttributeSet`); it does not break a socket. The developer's
reason: breaking the nearest part raises too many questions about what that means for the state of the game. What
was built: drift (`HazardDriftProcessor`, field hazards only), contact (`IShipContactQuery`), the impact, a
`NetworkTransformMediator` on the hazard prefab, `SkyHazardTests` cases, and scenario `HazardStrike`. The design below
is kept for reference.

- `DriftTowardShipMotion` and a pure `HazardDriftProcessor`.
- Streaming on the `FlyingCanUseCase` horizon rules (Enabled toggle, MaxAlive, spacing, removal distance).
- Ship contact:
  - Detection: an `OverlapSphere` against the ship.
  - Socket choice: `HazardContactProcessor.PickPointToBreak` takes the nearest healthy socket.
  - Response: `IShipBreakage.TryBreak`, optional through `TryResolve`. Then the impact cue and a despawn.
- Tests: the drift and contact processors, stream and contact cases (`FakeShipBreakage` already exists).
- Scenario `HazardStrike`: streaming off → `SpawnHazardAt 0` → `PointBroken 0`; a second hazard is shot down first.

## S3: Polish and tuning
- Owner-predicted muzzle flash.
- Crosshair and HUD reload line.
- Late-join scenario.
- Tune, then switch hazards on in `Profile_FuelSandbox`.

## Later (not in this plan)
- **S4 rifle** on the same path: `ITEM_Rifle` → `GA_FireRifle` → `ITargetingService` (`TD_RifleShot`) → `GE_RifleHit`, with deterministic spread seeded from (entity, tick) and no rewind until fast independent targets exist.
- **Helm moves to occupancy**, as its own plan.
- Ammo, and a damageable cannon (`State.Damaged` blocks `GA_FireCannon`).

## Docs (in the same change)
- `CODE_MAP.md`: feature index rows for Cannon and Sky hazards, configs, and trap lines (occupancy vs possession).
- `ARCHITECTURE.md`: §4 occupancy vs possession, §6 segment queries, the GAS flags.
- `NETWORK_TEST_HARNESS.md`: area row.
- `designed-events.md`: E2 and E3 are covered here.

## Verification (per slice)
- After each C# edit, check diagnostics and request Unity compilation, with zero errors.
- The EditMode suite stays green.
- `.\.tools\verify.ps1 -Scenario CannonShot` (S1) and `-Scenario HazardStrike` (S2) pass solo and host + client.
- Human playtest: aim feel on a turning ship, camera, reload pacing, ball blend, hazard readability.
