Status: Approved (2026-10-31: S1 and S2 with a placeholder body; S3 asks again. 2026-11-23: movement-only S1 + S2 built, see Progress); sixth in [core-systems-roadmap.md](core-systems-roadmap.md)

# Crewmates you can read: a body that moves, holds and works

## Why
In first person you see your crewmates, not yourself. Today they are capsules that slide, so you cannot tell who is
at the helm, who carries a crystal or who is about to fall. Readable bodies are core to a co-op game, and they depend
on facts the humanoid already has.

## Today (verified 2026-10-31)
- `NetworkPlayer.prefab`: a `Visual` child with a `MeshFilter` (a capsule). No `Animator`, no `SkinnedMeshRenderer`.
- `HumanoidVisualSmoothingView` (`Core/Humanoid/`) draws `Visual` at the interpolated pose while the root stays at the
  simulated one. Held items hang under `Visual/HeldItems`. The first-person rig draws the local body shadow-only.
- Facts every peer already has: the drawn pose (and so its velocity), ground data (`IHumanoidMovementView.CurrentGround`),
  look pitch (`LookPitch`), gameplay tags (station occupied, overboard, hooked), what is held (`ItemDefinition`).
- `AnimatorBoolCueHandler` (`Core/Gas/Cues/Handlers/`) can already set an Animator bool from a cue tag. The only
  animator controllers are the aether equipment idles (`Assets/Animations/AetherEquipment/`).

## Design
It follows the camera rig pattern: core publishes the facts, a feature draws them, test profiles can keep capsules.

### Core: the facts (`TinCan.Humanoid`)
- `IHumanoidPresentationState` (Core.Domain or Core.Humanoid): `LocalVelocity` (ship-local, measured on the drawn
  pose, so a player standing on a moving ship is not running), `IsGrounded`, `LookPitch`, `Stance` (standing,
  at station, hanging, reeling, overboard) and `HeldPose` (an id from the held item).
- `HumanoidPresentationProcessor` (pure, tested) builds it each frame from what is listed above. No new replication.
- `ItemDefinition` gets a `HeldPose` id (`none`, `two_hand_pole`, `one_hand_tool`, `carry_crystal`). A station gets
  one as well (`helm`, `cannon`).

### Feature: the animated body (`TinCan.Features.CrewAnimation`)
- A rigged humanoid model and an Animator controller: a locomotion blend tree (speed, direction), jump/fall, an upper
  body layer for held poses, a spine aim layer from pitch, and station poses.
- `CrewAnimationPresenter` sets Animator parameters from `IHumanoidPresentationState`. It swaps the capsule for the
  model at spawn, keeps `Visual/HeldItems` on the right hand bone, and keeps the first-person rig's shadow-only rule.
- Animator culling: `CullUpdateTransforms` for crewmates off screen.
- Listed in `Profile_FuelSandbox`/the main profile; test profiles keep capsules unless a scenario needs the body.

### Later: hands on things
Hand IK on the helm wheel, the cannon's handles and the winch crank, and foot placement on stairs. That needs the
Animation Rigging package (a new dependency) and is its own slice.

## Slices
1. **S1 Facts:** the presentation state, the processor, the held and station pose ids. Tests with
   `FakeMovingGround` (a ship moving under a standing player gives zero local speed).
2. **S2 Body:** model, controller, presenter, swapped in on the main profile. A scenario step checks that a crewmate
   at the helm reports the `helm` stance on the client (presentation probe).
3. **S3 Hands:** Animation Rigging IK (after a yes on the package).

## Decisions (developer, 2026-10-31)
1. ~~Unity's Starter Assets humanoid as the placeholder.~~ Replaced 2026-11-23: **a body built by a Blender script**
   (`.tools/blender/crew_body.py`, like the rotor mast) on a skeleton with Unity's humanoid bone names, imported as a
   humanoid. It stays in the repo and on style, and real art on the same rig can replace it without touching the
   presenter.
2. **Test profiles keep capsules**; the body is on the main profile.

Open: the Animation Rigging package for S3 (asked when S3 starts).

## Verification
- EditMode suite green, plus the processor tests.
- `verify.ps1 -UpTo Tests`; play runs of `HookAboard` and a helm scenario solo and host + client (asked for first).
- Human: watch crewmates walk on a turning ship, take the helm, carry a crystal, fall overboard; check the first-person
  shadow and held items.

## Progress
- 2026-11-23, movement only (developer: "a very simple player character model with animation cycles for its current
  movement"). Stances, held poses, the upper-body and aim layers and station poses are still open.
  - **Model:** `.tools/blender/crew_body.py` writes `Assets/Models/Characters/CrewBody/crew_body.fbx` (manifest beside
    it): a chunky 2 m crewmate made of rounded parts, each rigidly skinned to one bone, with Idle, Run (7 m/s), Sprint
    (12.6 m/s), Jump and Fall clips. The leg swing is sized to the ground speed, so the feet skate little.
  - **S1, slimmed:** `Core/Humanoid/HumanoidPresentationProcessor` (velocity against the ground underfoot, smoothing,
    grounded with a short grace). No `IHumanoidPresentationState` yet: the body measures for itself until a second
    consumer (footsteps) needs the facts.
  - **S2:** feature `CrewBody` (`Features/CrewBody/`). `CrewBodyUseCase` puts `Prefabs/CrewBody/CrewBody` under each
    humanoid's visual root as it registers and switches the capsule off. `CrewBodyView` probes the ground under the
    drawn feet (a ship's rigidbody is the frame) and drives the Animator (`Animations/CrewBody/CrewBody.controller`:
    Move blends on Speed, with the run reversed when backing; Air blends on VerticalSpeed). **TinCan > Dev > Crew Body >
    Build Assets** builds all of it and lists the installer in `Profile_Galleon`. Core change:
    `IHumanoidVisualAnchor.VisualRoot`, and `BodyRenderers` now includes the visual root's children, so the
    first-person rig hides the body.
  - Possession takes the camera (`OnNetworkSpawn`) before the body registers (`OnNetworkPostSpawn`), so
    `CrewBodyUseCase` calls `LookView.RefreshBody()` after attaching. `FirstPersonViewRig` now hides the body before
    it attaches its overlay: in test scenes the overlay throws from the third-party `VolumetricCloudsURP.Create`
    (`VolumeManager.instance.stack` is null), which left the body drawn. That third-party error is still open.
  - The model faces +Z because the armature object is turned before export (Unity reads the FBX axis header, so the
    export axes alone cannot turn it); `DevTools/Editor/Tests/CrewBodyModelTests` checks the facing and height.
  - Scenario `CrewBodyMoves` (Test_Galleon) passes Solo and Duo. Held items still hang from the visual root, not the hand; strafing plays
    the run forward.
