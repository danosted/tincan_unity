Status: Approved (2026-10-31: S1 and S2 with a placeholder body; S3 asks again); sixth in [core-systems-roadmap.md](core-systems-roadmap.md)

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
1. **Unity's Starter Assets humanoid as the placeholder** (free, humanoid rig, locomotion clips). A humanoid rig lets
   real art replace it later without touching the presenter.
2. **Test profiles keep capsules**; the body is on the main profile.

Open: the Animation Rigging package for S3 (asked when S3 starts).

## Verification
- EditMode suite green, plus the processor tests.
- `verify.ps1 -UpTo Tests`; play runs of `HookAboard` and a helm scenario solo and host + client (asked for first).
- Human: watch crewmates walk on a turning ship, take the helm, carry a crystal, fall overboard; check the first-person
  shadow and held items.
