Status: Approved

# Grappling hook

## Why
A fun tool to hold: hook onto any solid surface and pull yourself to it. The main use is getting back aboard after
falling off the airship. It also reaches higher decks and sky-island rock.

## Decisions (developer, 2026-10-14)
- **Controls:** hold Primary. Pressing it fires along the look (50 m). Holding it reels you in, and releasing lets go.
  You also let go on arrival, when the line snaps, or when you put the hook away. Swinging on a rope is not part of it.
- **Getting it:** from a rack on deck, like the repair tool (one item in hand at a time).
- **The line is seen by every peer**, replicated through a ship fixture. The player prefab is not touched.

## Design
- **Core seam:** `Core/Humanoid/IHumanoidMotionContributor.cs`. After the body's own momentum, jump and gravity, and
  before the move, a contributor may change that step's world velocity (`HumanoidMotionStep`). It runs on the server
  and on the predicting owner, and also during replay (`IsReplay`), where it must not change state. The movement use
  case keeps the result as momentum. The step also carries the body's `Momentum` from before its own braking and
  gravity, and the hook reels from that. The prefab's braking (75 m/s² then, 200 now) is stronger than the reel's acceleration, and
  starting from the braked velocity stalled the pull, as the first scenario run showed. The hook is the seam's first
  user.
- **Prediction:** the fire bit (`Input_Primary`) and the carrying tag (`State.Carrying.GrappleHook`) are already in the
  predicted stream. So owner and server each cast the same ray from the same input (eye height, input look) and agree
  on the anchor without an RPC. An anchor stores the input sequences it covered, so a replay pulls only for those.
- **Anchor:** fixed to the transform it hit, so a point on a flying ship moves with it.
- **Arrival:** within `ArriveDistance` the speed is clamped to `ArriveSpeed` and a hop (`ArriveHopSpeed`) carries you
  over a rail or a ledge.
- **Replication:** after humanoid movement the server writes the lines to the GrappleHookState fixture
  (`GrappleHookStateNetworkMediator`, a `NetworkList`) only when they change. A point on a networked object is stored
  in that object's local space, so each peer draws it on its own view of the ship.
- **Drawing:** `GrappleLinePresenter` runs per frame on every peer. The local player's line comes from its own
  simulation. Other players' lines come from the replicated state (on the server, from its own simulation).

## Assets
Built by **TinCan > Dev > Grapple Hook > Build Assets** (`DevTools/Editor/GrappleHookAssetBuilder.cs`):
- the tag and effect, `ITEM_GrappleHook` (id 4) and its held visual `Carry_GrappleHook`;
- the rack, `IA_TakeGrappleHook` and the state prefab, with their fixtures;
- the config, the materials and the installer;
- `Profile_FuelSandbox`, and `Profile_Test_GrappleHook` with its scene `Test_GrappleHook`.

## Progress
- [x] S1: hook, reel, rack, replicated line, EditMode tests, scenario `HookAboard` (2026-10-14; solo passes, host + client pending).
- [x] S2: aim marker (2026-10-14; EditMode tests pass, not yet run in Play). While you hold the hook and are not hooked, `GrappleAimPresenter` shows a marker
  where the next press would go: green within `MaxRange`, red up to `MarkerSightFactor` times further, hidden when nothing
  is in sight. It runs the hook's own query from the eye along the live look. Scenario step `HookAim InRange` in `HookAboard`.
- [ ] Playtest: tune feel (`ReelSpeed`, `ReelAcceleration`, the arrival hop), the held pose and the rack's spot on the
  airship (`(-3, -3.49, 2.5)`, next to the repair-tool rack).
- [ ] Later, if wanted:
  - a fire/attach cue (sound) through gameplay cues;
  - a rack fitting for designed ships (the fixture still spawns at its fixed pose there);

