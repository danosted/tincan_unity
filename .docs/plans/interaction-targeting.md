Status: Approved (2026-10-03)

# Interaction targeting that follows your look, and an outline on the target

## Why
Interacting feels off at some angles and distances, and some objects feel like their interaction point is in the wrong
place. Players also cannot see what E will act on. The query should pick what the player looks at, and the target
should show it.

## What is wrong today
`TD_Interact` is an `EyeAim` cone: 3 m range, 100 deg wide, 120 deg tall, nearest candidate wins, no line of sight.
1. **Targets are points at their pivot** (`IInteractionTarget.AimPoint` defaults to `transform.position`,
   `Core/Interaction/InteractionRequest.cs`). Range and angles are measured to it, and most pivots sit at deck level at
   the object's base (the cannon, the helm). Looking at the top of a tall object or standing beside a wide one misses.
2. **Nearest wins inside a wide cone**, so something closer off to the side beats what you look straight at.
3. **It faces the body, not the look.** The body slerps toward the look (`HumanoidMovementUseCase`), so after a quick
   turn the query points elsewhere for a few frames; first person makes this visible.
4. **No line of sight**: targets through walls and decks.

## Decisions
- **Outline only a target's own meshes** (developer, 2026-10-03). A target with no renderer of its own shows no outline:
  that is a badly made target to fix, not something to work around.
- **The helm becomes a proper fixture** (developer, 2026-10-03): the `HelmStation` prefab carries its own wheel and
  stand, and the airship model instance drops `StylShip_Wheel` and `StylShip_WheelStand`.
- **Outline technique: screen-space** (recommended, awaiting confirmation). A URP renderer feature draws the target's
  renderers into a mask, then traces the mask's edge on screen. Constant width on any mesh, no material changes; written
  against URP 17's render graph. The alternative, an inverted hull, breaks on the ship's hard-edged meshes.
- **Keep a narrow cone fallback** (recommended, awaiting confirmation) for small things the ray misses. The other choice
  is ray only: the most predictable, but harder at your feet.

## Target design

### Targeting (core, `Core/Targeting/`)
- **Look first:** a ray (thin sphere cast) from the eye along the exact look. A hit on an interactable within reach is
  the target, whatever its pivot. Range is measured to the hit.
- **Then forgive:** if the ray hits no interactable, a narrower cone (start at 30 deg wide, tune in play). Candidates are
  ranked by angle off the look, then distance, measured to the **nearest point of the target's collider** to the look
  ray, not its pivot.
- **Line of sight** in both cases: solid geometry between the eye and the point blocks, except the target's own
  colliders.
- **From the look, not the body:** `HumanoidTargeter` builds its origin from the input's look yaw and pitch (the
  replicated `LookRotation` in the platform frame, `LookPitch`) instead of the body transform. Owner and server still run
  the same query from the same input, so the server's answer stays authoritative and nothing new is sent.
- **As data:** a new shape (`LookThenCone`, or `Ray` with a fallback cone on `TargetingDefinition`), with the closest
  point coming from the targetable's colliders (`ITargetable` gains an optional collider set; the default finds its
  colliders). `TD_Interact` switches to it. Other definitions (repair, cannonball sweep) are unchanged.

### Outline (feature `TinCan.Features.TargetOutline`)
- **Local only, never replicated.** `TargetOutlinePresenter` (every frame, local player) reads the Interact prompt's
  current target (`InteractorControllerView.CurrentTarget`), so the outline and what E does always agree.
- **What is drawn:** the target's own renderers (in its hierarchy, stopping at a nested entity), switched into an
  outline rendering layer while targeted. None, no outline.
- **How it is drawn:** an `OutlineRendererFeature` on `_MainURPRenderer` (mask pass, then edge pass), with colour and
  width in a `TargetOutlineConfig`. Validate it with the `validate-urp-render-graph-renderer-feature` skill.
- Its installer goes in `Profile_FuelSandbox` and the test profiles that interact (Core, ShipDamage, Cannon, Helm).

### The helm as a proper fixture (`Features/Helm/`)
- `HelmStation.prefab` gets `Wheel` and `Stand` children using `StylShip_Unity.fbx`'s meshes and material, placed so they
  sit exactly where the ship's do today. Its interaction collider fits the wheel.
- `Airship_Prefab` removes `StylShip_Wheel` and `StylShip_WheelStand` from its model instance (prefab overrides; the FBX
  is untouched). The stand base the helmsman stands on moves with them.
- The test ship gains a real wheel for free.
- `HelmAssetBuilder` builds the new prefab (delete and rebuild once; the network id changes, which is fine before
  release).
- Later, not in this plan: the wheel turns with the yaw input.

## Slices
1. **Helm as a proper fixture.** Prefab, ship override, builder. `HelmSteer` must still pass.
2. **Targeting:** look-first query, closest-point candidates, line of sight, look-based origin. `TD_Interact` switched.
   EditMode tests for the processor (ray wins over a nearer cone candidate; closest point beats pivot; occluded target
   rejected). Every interaction scenario must still pass (CannonShot, HelmSteer, InteractRack, RepairLoop, NetCatch,
   EquipCycle).
3. **Outline:** renderer feature, presenter, config, installer. A scenario probe that the local target's renderers are on
   the outline layer, plus a checkpoint capture; the look itself needs a human eye.
4. **Docs:** `ARCHITECTURE.md` §6 (targeting), `CODE_MAP.md` feature row, the `TD_*` notes.

## Open questions
- The two recommendations above (screen-space outline; keep a narrow cone fallback).
- Outline colour and width; whether it pulses.
- Should the prompt text ("E: Take helm") sit by the outline? The current prompt stays as is unless you want this.

## Progress (2026-10-03)
All four slices built; both recommendations taken (screen-space outline, narrow cone fallback).
- **Helm:** `HelmStation` carries `Wheel` (box collider: what Interact sees and what is outlined) and `Stand` (mesh
  collider, stood on) from the ship FBX at the model's exact poses; `HelmAssetBuilder` switches the airship model's
  `StylShip_Wheel` and `StylShip_WheelStand` off (an override; the FBX is untouched).
- **Targeting:** `TargetShape.Look` (`TargetingUseCase.GatherLookHit`, then `GatherClosestPoints`), an accept filter on
  `ITargetingService.TryAcquire`, `HumanoidTargeter` facing the replicated look. `TD_Interact`: `CameraAim`, `Look`,
  range 2.5 m, ray radius 0.05, fallback cone 30 x 30 deg, best aligned, line of sight.
- **Outline:** `Features/TargetOutline/` with `TargetOutlineRendererFeature` on `_MainURPRenderer` and
  `Shaders/TargetOutline.shader`; installer in `Profile_Base` and `Profile_Test_Core`. Hidden while the Humanoid context
  is off (stations, menus), so it never shows what E would not act on.
- **Scenarios:** the face commands now aim at the target's collider centre (`ScenarioAim`), as a player looks at it;
  InteractRack, CannonShot and HelmSteer check `TargetOutlined` and capture a checkpoint.
- **Needs a human eye:** outline colour and width, the 30 deg fallback, and the reach (2.5 m to the surface).
