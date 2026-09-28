Status: Done (2026-09-28)

# Composition root diet (slice 5 of `architecture-review.md`: B5, B7)

## Context
Verified at the slice 4 commit. `ProjectLifetimeScope` (49 registration calls) still wires:
- **Core** (stays; these are the pillars the scheduler drives): networking, time, registries and orchestrator,
  possession, abilities, input, humanoid and airship movement, the scheduler, interaction core (the orchestrator needs
  `IVehicleBoardingUseCase`, so boarding stays core), and the spawning of player, airship and possession mediator
  (moves with the session layer, a later plan).
- **Direct features** that belong in installers: cloud boundary (with the two cloud configs as scope fields), gas
  challenge, free camera, the door handler (with its tag as a scope field), and coordinated events.
- **Global lookups the pillars forbid:** `Camera.main` in `CloudEnvironmentView`, `NetworkManager.Singleton` in
  `ThirdPersonLookView`, and a per-frame `FindObjectsByType<GasPocketVolume>` in `GasChallengeUseCase`, which is also
  server gameplay on the per-frame tick (B8).
- **Coordinated events are dead:** `EventOrchestratorUseCase` only acts on `TriggerEvent`, whose only caller
  (`CoordinatedEventMediator.TriggerEventRpc`) was deleted in slice 3; the mediator is on no prefab or scene.

## Design
1. **Installers** (in `Features`, assets in `Resources/Installers`, listed in `Profile_Base` and the test profiles
   that need them):
   - `CloudBoundaryFeatureInstaller`: `CloudBoundaryConfig`, `CloudVisualProfile`, processor, surface query, expiry
     handler, `CloudEnvironmentView`, and `CloudBoundaryUseCase` as an `ISimulationTickable` in a new phase
     `BeforeHumanoid` (after `AfterAirship`, before the physics sync), which is exactly where the scheduler calls it
     today. The scheduler loses its explicit cloud call.
   - `GasChallengeFeatureInstaller`: `GasChallengeUseCase` as an `ISimulationTickable` (`AfterAirship`). Pockets are
     found by a physics overlap around each ship, not by scanning the scene.
   - `FreeCameraFeatureInstaller`: the two processors and `FreeCameraMovementUseCase`.
   - `AirshipDoorFeatureInstaller`: `DoorInteractionHandler` with its tag.
2. **`ILocalViewCamera`** (`Core/Domain`): the camera the local player looks through (the possessed actor's orbital
   camera). Implemented in `Features/Possession` from `IPossessionState`. `CloudEnvironmentView` uses it.
3. `ThirdPersonLookView` reads the local client id from the injected `INetworkService`.
4. Coordinated events: per the developer's decision below.
5. Scene values move from `GameLifetimeScope.prefab` to the installer assets (cloud configs, door tag), done in the
   Editor so no reference is lost.

Out of scope (own slices): splitting `HumanoidPlayer` and moving Assembly-CSharp logic into `Features` (B6); spawning
into a session installer (session plan); `GASVisualScriptingBridge`.

## Tests
- `GasChallengeUseCaseTests`: a ship overlapping a pocket detonates it once; a distant pocket does not.
- `SimulationTickRunner`: `BeforeHumanoid` runs between the other two phases.
- Rules baseline: the global-lookup list becomes empty; `GasChallengeUseCase` leaves the untested list; the scope
  limit drops.
- `.\.tools\verify.ps1 -All`. Human playtest: fly into clouds (boundary push, submersion visuals), a gas pocket, the
  door, the free camera.

## Docs
- `ARCHITECTURE.md` §7 and `CODE_MAP.md` feature index (direct → installer; the core list); `FEATURE_INSTALLERS.md`
  (the new phase). Review: B5 and B7 done.

## Outcome (2026-09-28)
- Coordinated events deleted (developer: keep only if sound enough to reuse for designed events with gunners and
  targets; it was a 70-line per-frame timer with one active event, no actions and no assets, so a new system will be
  designed). The empty `Features/Events/IShipState.cs` went with it.
- `ProjectLifetimeScope`: 49 → 37 registration calls; no scene values left besides spawning prefabs and the profile.
- The global-lookup baseline is empty: no `Camera.main`, `NetworkManager.Singleton` or `Find*` left in `Features`.
- `AirshipDoor` moved out of the stray `Assets.Scripts.Features.Airship` namespace; `IA_ToggleDoor`'s handler type
  name updated in the Editor with it. The door tag stays empty, as it was on the scope.
- The gas pocket's `NetworkObject` despawn was dropped: the `GasPocket` prefab has no `NetworkObject`.
- EditMode 451/451; all seven scenarios pass solo and host + client (6 min batch).
- Still for a human: fly into the cloud deck (push-out, submersion visuals now from `ILocalViewCamera`), a gas pocket
  (scene `cvg_gaspocket_test`), the door, the free camera.
