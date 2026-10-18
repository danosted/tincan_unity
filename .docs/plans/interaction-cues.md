Status: Draft

# Interaction cues, and the hammer held lower

## Why
Picking up the rod or the hammer, or taking the helm, gives no clear sign that it worked. Each interaction should
have a short, simple cue. Separately, in first person the hammer sits too high in the view. The rod's held pose looks
right, so the hammer should match it.

## What exists today
- **Gameplay cues** (`Core/Gas/Cues/`, plan `gameplay-cues.md`): a `GameplayEffectDefinition` has a `Cues` list. While a
  Duration/Infinite effect is active, its cue tags sit on the target. `GameplayCueUseCase` turns the tag appearing into
  the notify's `OnActive` actions, and this runs on every peer. The available actions are `PlaySoundCueAction`,
  `SpawnPrefabCueAction` and `HudToastCueAction`. Notifies are `Assets/Abilities/Cues/GCN_*.asset`, and a feature lists
  them on its installer.
- **The interaction moments already apply effects to the player:** an item pickup applies the item's `EquippedEffect`
  (`GE_Holding_Net`, `GE_Holding_RepairTool`, `GE_Holding_JerryCan`), and the helm applies `GE_Occupying_Helm` (the
  cannon applies `GE_Occupying_Cannon`). All of them have `Cues: []` today.
- **Interactions with no effect on the player** (door, pour fuel, flight boost, repair/damage module) only run their
  handler on the server. `IInteractionHandler.Handle` returns nothing, so nothing knows that the interaction succeeded.
- **`HudToastCueAction` has no peer filter:** a toast shows on every peer, not only for the player who interacted.
- **Held pose:** `EquipmentNetworkMediator` instantiates `ItemDefinition.HeldVisual` under the player's `Visual` and
  keeps the prefab root's local pose. Today these are:
  - `Prefabs/Items/Carry_Net.prefab` (the rod): position (0.45, 0.15, 0.65), rotation x 75 deg, scale 2.5.
  - `Prefabs/Items/Carry_RepairTool.prefab` (the hammer): position (0.45, 0.9, 0.55), rotation x 60 deg, scale 1.
  - The two have different pivots and scales, so copying the rod's numbers into the hammer does not give the same
    on-screen placement.

## Plan

### Slice 1: pickup and helm cues (assets only)
1. Add the cue tags `Cue.Player.Interact.PickUp` and `Cue.Player.Interact.TakeStation`, or one per item if each should
   sound different.
2. Add a `GCN_Player_Interact_*` notify for each. `OnActive` plays a short positional sound at the player (start with
   `Audio/Cues/cue_ding.wav`) and optionally shows a toast ("Picked up Hammer").
3. Put the cue tags on `GE_Holding_*` and `GE_Occupying_Helm` (and `GE_Occupying_Cannon`).
4. List the notifies on the installer of the feature that owns each moment (Items, Helm or Stations).
5. Add a cue probe to the existing scenarios `EquipCycle` and `HelmSteer`.

This slice needs no new C#, so it adds no tests. It is an Infinite-effect state cue, so it also fires again for a
player who joins late while someone holds an item. Check whether `OnActive` replays for a late joiner, and if it does,
keep the sound out of that replay.

### Slice 2 (optional): every interaction
1. `IInteractionHandler.Handle` reports success (a `bool`, or an outcome) and the existing handlers return it. Their
   EditMode tests cover it.
2. `InteractionDefinition` gets an optional `Cue` tag. After a successful handle, `InteractionOrchestrator` fires it as
   a burst at the target through `IGameplayCueDispatcher`.
3. Add cue tags and notifies for the door, pour fuel, flight boost and the repair/damage module.
4. With this slice, slice 1's pickup and helm cues could move onto their `IA_*` assets instead, giving one mechanism
   rather than two.

### Slice 3: hammer held pose
1. Move the `Carry_RepairTool` root lower, to the rod's height and angle (rotation x 75 deg).
2. Tune it in the Editor against a first-person capture next to the rod's, so both sit in the same spot on screen.
3. Then run the `EquipCycle` and `RepairLoop` scenarios.

## Open decisions (developer)
1. **Scope:** slice 1 only, or slice 2 as well?
2. **Kind of cue:** sound only, sound plus a toast, or also a small VFX at the object?
3. **Who sees it:** the sound is heard by everyone nearby. Should the toast show only for the player who interacted?
   That needs a role filter on `HudToastCueAction` (`GameplayCuePeerRole` exists in `Core/Domain/Cues/`).
4. **"The rack is placed perfectly":** read here as the rod (`Carry_Net`), so the hammer copies the rod's pose.
   Confirm.

## Done when
- Picking up the rod, hammer or jerry can, and taking the helm, each play their cue on host and client.
- The hammer sits where the rod sits in first person.
- `.\.tools\verify.ps1` passes for the touched scenarios.
- `CODE_MAP.md` lists the new cues in the Gameplay cues row.
