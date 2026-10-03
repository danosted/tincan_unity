Status: Approved (2026-10-03; replaces the parked 2026-09-29 "possession stays" plan)

# Immersive piloting: swappable camera rigs and the helm as a station

## Why
Possessing the airship pulls the player out of their body: the view jumps to the ship's orbit camera and the body is
left behind. Players should stay in their own body and their own view, including at the helm. Two changes:
1. The camera rig (third person, first person) becomes a feature that a profile picks.
2. The helm becomes a crew station like the cannon. Possession no longer steers the ship.

## Decisions (developer, 2026-10-03)
- **Reverse the possession helm.** The concern behind it, that AI should be able to fly the same ship, is met by a pilot
  input seam on the airship (below), not by possession.
- **Rigs are features, each in its own assembly.** No composition rule: the first registered rig wins, and a warning is
  logged when more than one (or none) is registered. Both rigs implement one core contract.
- **Remove the old possession helm** outright, so core keeps a single airship input path.
- **Pitch stays on Shift and Space** (today's `Airship/Pitch` binding), now in the helm context.
- The free camera keeps using possession; it is the only possessable left besides the body.

## Part A: camera rigs

### Today
`Core/Humanoid/ThirdPersonLookView.cs` mixes two things:
- **Look state** (yaw, pitch, sensitivity, max pitch). This is simulation input: it becomes `LookRotation` and
  `LookPitch`, and the platform yaw carry (`HumanoidMovementUseCase.ApplyPlatformYaw`) turns it with the ship.
- **Camera placement:** the orbit, in `LateUpdate`.

`HumanoidPlayer` and `AirshipControllerView` both `[RequireComponent]` it.

### Target
- **Core keeps the look state** on the player prefab: today's `IOrbitalLookView` without the orbit, renamed to fit,
  e.g. `ILookView`, with `IHasOrbitalCamera` becoming `IHasLook`. It also keeps the one `Camera` and its
  `AudioListener` on the prefab, and the possession on/off switching. `ILocalViewCamera`, `StationViewPresenter` and
  the scenarios that call `ApplyLook` are unchanged apart from the names.
- **Core contract** `IViewRig` in `Core/Domain/Look/`:
  - `void Place(Camera camera, ILookView look, Vector3 visualPosition, Quaternion bodyRotation)`: called every frame
    for the local player's camera.
  - `float AimHeight(IHumanoidMovementView body)`: the height above the root that `CameraAim` targets from.
    It replaces `OrbitHeight` and is the one simulation-relevant value a rig owns. Every peer, the server included,
    loads the same profile, so they agree.
- **Core driver** `ViewRigPresenter` (a `LateUpdate`-time tick, local player only):
  - resolves `IReadOnlyList<IViewRig>` and uses the first;
  - logs a warning once if more than one rig is registered, or if none is (then the camera stays where the prefab
    puts it);
  - feeds the rig `IHumanoidVisualAnchor.VisualPosition`, never the simulated root, so the view does not step at
    tick rate.

  `HumanoidTargeter` asks the same selected rig for `AimHeight`.
- **`TinCan.Features.ThirdPersonCamera`:** today's orbit math moves here. Distance and height go into a
  `ThirdPersonCameraConfig`. `AimHeight` is the orbit height, so behaviour is unchanged.
- **`TinCan.Features.FirstPersonCamera`:**
  - The camera sits at the visual position plus `EyeHeight`, rotated by look yaw and pitch. `AimHeight` equals
    `EyeHeight`, so `CameraAim` coincides with `EyeAim`.
  - `FirstPersonCameraConfig` holds the near clip and field of view.
  - The local body is drawn shadows-only (renderers under `Visual` set to `ShadowCastingMode.ShadowsOnly` for the
    local player only); proxies are unaffected.
  - Open: whether held items (net, jerry can) need a separate view layer. Check in the first playtest.
- **Profiles:** `Profile_Base` (and every test profile that does not include it) lists one rig installer. Third person
  stays the default until playtests decide; first person is tried by swapping the installer in `Profile_FuelSandbox`.
- **The airship loses its look in slice 4,** together with possession. Until then it keeps `ThirdPersonLookView`
  (its own orbit, ship-only), so the possession helm stays usable between slices. Slice 4 deletes that class, and
  `AirshipNetworkMediator` stops forwarding `Look`.

## Part B: the helm station

### Today
- `Core/Ship/AirshipControlPanel.cs` is an `IVehicleBoardable`. Interacting with it calls
  `IPossessionAuthority.TryAcquirePossession`, which transfers ownership of the ship.
- The pilot's client then writes `AirshipInputState` into an owner-write `NetworkVariable` on `AirshipNetworkMediator`.
- `AirshipMovementUseCase` simulates on the server and zeroes the input without a possessor.

### Target
- **Pilot input seam (core, `TinCan.Ship`):**
  - `IAirshipPilotInput` (server): `bool TryGetInput(IAirshipView ship, out AirshipInputState input)`.
  - `AirshipMovementUseCase` reads steering from it, through an optional resolve; with no source the ship coasts to
    zero input.
  - The ship's owner-write input `NetworkVariable`, its `PossessableNetworkMediator`, `IPossessable` forwarding and
    the possessor check go.
  - An AI helmsman later is another implementation of this seam.
- **`TinCan.Features.Helm`** (references Stations, Ship, Humanoid), installer `HelmFeatureInstaller`:
  - **`HelmNetworkMediator : IStation`,** a ship fixture spawned like the cannon, with:
    - a `Seat`;
    - `OccupyAbility = GA_OccupyHelm` (an Infinite effect with `State.Occupying.Helm` that locks walking);
    - no granted abilities;
    - `ViewCamera = null`, so the helmsman keeps their own rig's view.

    The interaction definition is `IA_TakeHelm`, handled by the existing `OccupyStationInteractionHandler`.
  - **`HelmsmanInputContext`** (`Context_Helmsman`):
    - live while the local body has `State.Occupying.Helm`;
    - blocks the Humanoid context but **not** the Camera context, so the helmsman looks around freely while steering;
    - actions: Throttle (W/S), Yaw (A/D), Pitch (Space/Shift, today's binding moved over), Leave (the Interact
      key, routed as the Interact ability bit like the cannon's Leave).
  - **`HelmInputUseCase : IHumanoidInputContributor`** (owner) writes the three axes into the helmsman's predicted
    `HumanoidInputState`, in a new `StationAxes` field (`Vector3`, zero off-station) next to `StationAim`.
    It is serialized with it and replayed with it, so it costs nothing new on the wire beyond 12 bytes.
  - **`HelmSteeringUseCase : IAirshipPilotInput`** (server): for each helm, the occupant's last simulated input's
    `StationAxes`, mapped to `AirshipInputState`. It returns nothing when the helm is empty.
- **Feel:** the ship is still simulated on the server with one-way input latency. The airship ticks before the
  humanoid, so a helm input applies one tick after it is simulated. Predicting the ship is out of scope; the old
  slice-2 notes remain the path if steering feels laggy.
- **Removed:**
  - `AirshipControlPanel` (and its `ArchitectureRulesBaseline` entry);
  - `IVehicleBoardable`, `IVehicleBoardingUseCase`, `VehicleBoardingUseCase`, `ExitVehicleInputHandler`;
  - the airship branch in `InteractionHandlers`;
  - the possessed-Ship input context and `PossessedActorKind.Ship`, if nothing else uses them;
  - the ship's camera.

  The `Airship/*` input actions move into `Context_Helmsman`; regenerate `INPUT_MAP.md`.
- **`DevTools/BotRouteUseCase`** (perf bot) occupies the helm through `IStationOccupancy` instead of possessing the
  ship.

## Slices
1. **Look and rig split, third person only.** Core contract, the player's `LookView`, `ThirdPersonCamera` feature,
   profiles updated. The ship keeps its own orbit until slice 4. No behaviour change: existing scenarios (Targeting, Cannon) must still pass.
2. **First-person rig.** The feature, shadows-only local body, config. Swapped into `Profile_FuelSandbox` for a
   playtest.
3. **Pilot input seam.** `IAirshipPilotInput` in core. Possession steering is still in place behind a temporary
   adapter, so the ship stays drivable between slices.
4. **Helm station.** The feature, context, contributor, steering use case, fixture prefab and definition. The old
   possession helm, adapter and boarding code are removed. The perf bot moves over.
5. **Docs:** `ARCHITECTURE.md` §4 (possession is the free camera only; the helm is a station), `CODE_MAP.md` feature
   index rows (ThirdPersonCamera, FirstPersonCamera, Helm) and the "Airship movement" and "Possession" rows,
   `INPUT.md` and `INPUT_MAP.md`.

## Tests
- EditMode:
  - **Rig selection:** first wins; a warning on more than one or none.
  - **Each rig's `Place` and `AimHeight`.**
  - **`HelmInputUseCase`:** axes on-station, zero off-station.
  - **`HelmSteeringUseCase`:** maps the occupant's axes; empty helm returns nothing; a gone occupant returns nothing.
  - **`AirshipMovementUseCase`** with and without a pilot source.
  - **`HumanoidInputState` serialization** round-trips `StationAxes`.
- **Scenario `HelmSteer`** in a test-range scene (solo, and host + client):
  - take the helm; throttle forward and yaw;
  - assert the ship moved and turned on the server and the helmsman's body stayed on the seat;
  - leave, and assert the ship coasts to zero input and the body can walk again.

  This also covers the "input clears on release" path that was never tested.
- **Existing scenarios** (Cannon, Targeting, Voyage, Boarding) stay green under the third-person rig.

## Open questions
- Does the first-person rig need its own scenario, or is the rig-selection EditMode test plus the Targeting scenario
  run under `FirstPersonCamera` enough?

## Settled
- The helmsman looks around as freely as when walking: no clamp relative to the seat.
- The cannon keeps its own `ViewCamera`, a first-person sight down the barrel (iron sights), under either rig.

## Progress (2026-10-03)
All slices built in one pass (slice 3's temporary possession adapter was not needed). EditMode suite green (713).
- **Camera:** `LookView` on the player, `IViewRig` + `ViewRigSelection` in core; `ThirdPersonCamera` (test profiles) and
  `FirstPersonCamera` (`Profile_FuelSandbox`, the main game) features. Hiding the body draws its own renderer shadow-only;
  held items (children of `Visual`) stay visible.
- **Helm:** `Features/Helm/` as planned; `HelmStation` fixture at the wheel (no visuals, the ship model has the wheel).
  The test ship got a quarterdeck at the airship's height so the same fixture position works on both.
- **Removed:** possession of the ship (its `PossessableNetworkMediator`, camera and look), `AirshipControlPanel`,
  `IA_AcquireAirshipControl`, vehicle boarding, `ExitVehicleCommand`/handler, `Context_Airship`,
  `PossessedActorKind.Ship`. Fuel now burns on the pilot's throttle alone (no possessor check).
- **Open:** the `HelmSteer` scenario and the existing ones under the new camera still need their first play run
  (solo and host + client), and a human playtest of first person at the helm and at the cannon.
