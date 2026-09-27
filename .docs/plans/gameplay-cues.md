Status: Approved

# Gameplay cues: composable, tag-identified, contributed by features

## Context

Step 3b of `ship-damage-repair.md` (section D). Break and repair work but are hard to read: the only feedback is the
orange marker, which `ShipDamagePointNetworkMediator` toggles itself from the replicated health attribute.

This slice adds a cross-cutting cue layer: sound, VFX, HUD and animation feedback for gameplay moments. It follows
the same decoupling as the rest of the project:
- A cue is **identified by a gameplay tag**.
- What a cue **does** is composed from small, reusable parts.
- **Features contribute their cues through their `FeatureInstaller`**, the way they contribute ship fixtures. A cue
  exists only if its feature is in the scene's profile.

The first draft had a fixed set of fields per cue (VFX, clip, HUD text), a fixed binding menu on `TagCueView`, and
a second id database next to the tag registry. On review with the developer it was too hard-coded, and this rewrite
replaces it.

The split stays:
- **Domain events** (`IEventPublisher`): server-local logic, telemetry and tests. They never drive presentation.
- **Gameplay cues**: presentation only, on every peer. They never change gameplay state.

## What the code says (carried over from the first draft)

| Section D assumed | Actually | Consequence |
|---|---|---|
| Tags already replicate, so state cues handle late join | Tags cross the network only as `SyncTagsClientRpc(name, added)` deltas (`Network/Infrastructure/Abilities/AbilityNetworkMediator.cs`). A late joiner gets no tags. The marker works for late joiners only because it polls the health attribute (`NetworkList`). | Tag replication must become state first: **slice 0, shipped on its own**. |
| A singleton `GameplayCueNetworkMediator` sends the burst RPC | No scene-level network singleton exists outside protected prefabs. Every cue target is a GAS actor with an `AbilityNetworkMediator` on a spawned `NetworkObject`. | Bursts travel as an RPC on the target's own `AbilityNetworkMediator`. |

Also confirmed:
- Owner prediction does **not** replay abilities on reconciliation (`HumanoidMovementUseCase.RewindAndReplay`
  re-integrates motion only), so a predicted cue fires once.
- The owner also applies effects outside prediction: `EquipmentAbilityBinder` applies `EquippedEffect` on the server
  **and** the owner. "The owner applied it" does not mean "the owner predicted it".
- "Repaired" is not one effect. It is the removal of `GE_ShipPartBroken` by `ShipBreakageUseCase.Reconcile`.

## Design

### Slice 0: tags as replicated state (prerequisite, own commit, own verification)
- In `AbilityNetworkMediator`, replace the server → client `SyncTagsClientRpc` deltas with a replicated set of
  present tags (decision B). Owner prediction (`_predictedEffectTagNames`) and the owner's optimistic
  `_clientActiveTagNames` stay as they are.
- A set of flags is enough: `ApplyEffect` and `RemoveEffect` already guard "another effect still grants this tag".
- Verify: `TagRequest`, `ShipDamage` and `RepairLoop` stay green. A late-join check proves a joining client sees
  the server's tags.
- Worth shipping even without cues: it fixes late join for everything that reads tags.
- **Done (2026-09-27, awaiting developer review):**
  - `AbilityNetworkMediator` replicates a `NetworkList<FixedString64Bytes>`; `SyncTagsClientRpc` is gone.
  - The client-side view is the pure `ClientTagState` (`ClientTagStateTests`).
  - The harness gained a delayed join: `-joindelay` in `CommandLineSessionBootstrap`, `Scenario.Builder.JoinLate`, and
    the probe `NoRemotePlayers`.
  - New scenario `ShipDamageLateJoin`: it failed before the fix (the late client lacked `State.Damaged`) and passes
    after it, solo and host + client.

### 1. Identity: a cue is a `Cue.*` gameplay tag
- For example `Cue.Ship.Part.Broken` or `Cue.Ship.Part.Break`, stored as tag assets in `Assets/Abilities/Tags/`.
- This reuses `IGameplayTagRegistry`, the tag replication from slice 0, and name-based identity on the wire. There
  is no second database.
- **Convention, documented and not enforced:** gameplay logic never reads `Cue.*` tags. They exist for
  presentation.

### 2. Sources: effects declare cues
- `GameplayEffectDefinition` gains `List<GameplayTag> Cues`.
- **Duration and Infinite effects:** their cue tags join the actor's tag set while the effect is active, next to
  `GrantedTags`.
  - That makes them **state cues**: they replicate as tag state (slice 0) and reach late joiners.
  - On the owner, predicted effects make them predicted.
  - Handlers get *Active* on the tag's rising edge and *Removed* on its falling edge.
- **Instant effects:** their cue tags fire **Execute** once, as a **burst cue** through the dispatcher and the RPC
  below.
  - Late joiners miss bursts by design; anything that must persist is a state cue.
- One model covers both kinds: the effect's duration type decides.
- Features may also fire a burst directly through `IGameplayCueDispatcher` for moments that are not effects. Avoid
  this where an effect fits.

### 3. Handlers: what a cue does (composable)
- **`IGameplayCueHandler`** (`Core/Domain/Cues/`, a contract shared by features): `GameplayTag Cue`, plus
  `OnExecute`, `OnActive` and `OnRemoved(in GameplayCueEvent)`.
  - `GameplayCueEvent` carries the target transform, its `IAbilityControllerBase`, and the peer role (server,
    owner or proxy).
- Handlers come in two kinds:
  - **Asset handlers, contributed by features.** `GameplayCueNotify` is a ScriptableObject
    (`Assets/Abilities/Cues/GCN_*.asset`) with a cue tag and three action lists: `OnExecute`, `OnActive` and
    `OnRemoved`.
    - Actions are small classes deriving from `GameplayCueAction` (decision A):
      - `PlaySound` (a one-shot clip at the target);
      - `SpawnPrefab`: in `OnExecute` it lives for its lifetime; in `OnActive` it lives until `OnRemoved`. It is
        pooled and parented to the target, so it is ship-local.
      - `HudToast` (through `IHudValues`);
      - later: `CameraShake`, `AnimatorTrigger`, and others.
    - A new kind of action is a new class; nothing existing is edited.
    - Features contribute notifies with `FeatureInstaller.IExtension<GameplayCueNotify>`. `GameplayCueCatalog`
      gathers them from `FeatureInstallerCatalog` exactly as `Features/Airship/Fixtures/ShipFixtureCatalog.cs` does.
  - **Handlers on the object.** Components implementing `IGameplayCueHandler` in the target's own hierarchy, for
    things that are part of the model: showing the damage marker, an Animator bool on a tool.
    - Small reusable components: `ToggleObjectCueHandler` (enable a child while active) and
      `AnimatorBoolCueHandler`.
    - They find their controller with `GetComponentInParent`, with no serialized component references.
- More than one handler per cue is normal: a bang notify plus a marker toggle, for example.

### 4. Runtime (`Features/Abilities/Cues/`, `GameplayCuesFeatureInstaller`, `Profile_Base`)
- **`GameplayCueUseCase`** (every peer):
  - watches cue-tag edges on registered GAS actors (through `IActorRegistry`) and calls `OnActive` / `OnRemoved`
    on the matching asset notifies and on-object handlers;
  - receives bursts and calls `OnExecute`.
  - Edge detection is a pure `GameplayCueStateTracker` (tested); the use case is a thin adapter.
- **Burst dispatch.** `AbilitySystemUseCase.ApplyEffect` calls `IGameplayCueDispatcher.Execute(cue, target,
  context)`. The pure, tested `GameplayCueDispatchProcessor` decides:

  | Peer | Predicted | Action |
  |---|---|---|
  | Server | no | play locally (host), send to all clients |
  | Server | yes | play locally (host), send to all clients except the target's owner |
  | Owner client | yes | play locally now |
  | Any client | no | drop; the server's RPC brings it |

- **Prediction is explicit, not ambient.** `ApplyEffect` takes a `GameplayEffectContext` (with `IsPredicted`), and
  `ProcessAbilitySimulation` passes `IsPredicted = true`. Every other caller gets the default (false), so the
  equipment binder is never taken for a prediction.
- **Transport:** `AbilityNetworkMediator.ExecuteCueClientRpc(string cueTag)` on the target, unreliable, with the
  owner optionally excluded. The target is the RPC's object, so position is implicit and ship-local.
- **Observability:** `IGameplayCueObserver` is notified of every handled cue. The scenario harness registers one
  for its probes, so no test concern lives in production code.
- **Without `GameplayCuesFeatureInstaller`,** `AbilitySystemUseCase` resolves no dispatcher. Cues then stay silent:
  no errors, no warnings.

### 5. First cues (all contributed by `ShipDamageFeatureInstaller`)

| Cue tag | Kind | Source | Handlers |
|---|---|---|---|
| `Cue.Ship.Part.Broken` | state | `GE_ShipPartBroken` (Infinite, on the point) | on-object `ToggleObjectCueHandler` on each socket's `Marker` (the point stops toggling it itself); notify `OnActive`: sparks prefab; `OnRemoved`: ding + `HudToast "Part repaired"` |
| `Cue.Ship.Part.Break` | burst | `GE_ShipPartBreak` (Instant) | notify `OnExecute`: spark burst + bang |
| `Cue.Ship.Leak` | state | `GE_HullBreach` (on the ship) | notify `OnActive`: `SpawnPrefab` with a looping hiss, parented to the ship |
| `Cue.Player.Repairing` | state | `GE_Repairing` (on the player, predicted) | notify `OnActive`: `SpawnPrefab` with a looping repair sound, parented to the player. No edit to `NetworkPlayer.prefab` is needed. |

Crude assets are fine: primitive particles and placeholder clips.

**Watch:** `OnRemoved` fires on *any* removal of the state, including cleanup when an actor despawns. Check whether
despawn removes effects one by one; the "Part repaired" ding must not play on teardown. If it does, add a removal
reason to the edge (`Removed` versus `Teardown`) and skip actions on teardown.

## Build order (each step green on `verify.ps1`)
0. Slice 0: tags as replicated state (own commit), with a late-join check.
1. Contracts and pure parts, with tests:
   - `IGameplayCueHandler`, `GameplayCueEvent`, `GameplayEffectContext`;
   - `GameplayCueStateTracker`, `GameplayCueDispatchProcessor`, `GameplayCueCatalog`.
2. Runtime:
   - `GameplayEffectDefinition.Cues`;
   - cue tags joining the tag set for Duration/Infinite effects;
   - the `ApplyEffect` context and the `Execute` hook;
   - the RPC, `GameplayCueUseCase`, and the installer in `Profile_Base`.
   - Split `GameplayEffectDefinition.cs` (enums and struct) into one type per file while touching it.
3. Actions and on-object handlers: `PlaySound`, `SpawnPrefab` (pooled), `HudToast`, `ToggleObjectCueHandler`,
   `AnimatorBoolCueHandler`, plus the action type-picker drawer (decision A).
4. The ship cues: tags, notifies, the prefabs, `IExtension<GameplayCueNotify>` on `ShipDamageFeatureInstaller`, and
   the marker moved onto its handler.
5. Scenario probes (through `IGameplayCueObserver`) and docs.

### Progress
- **Slice 0 done** (commit `3c5973b`).
- **Steps 1 and 2 done (2026-09-27, awaiting developer review).** EditMode 410/410. `TagRequest`, `RepairLoop` and
  `ShipDamageLateJoin` pass solo and host + client with the installer live.
- **Where the code differs from the design above:**
  - **Observers:** `IGameplayCueObserver` became `IGameplayCueFeed`, an event on the use case. VContainer does not
    promise an empty list when no observer is registered.
  - **Transport:** two interfaces carry it.
    - `IGameplayCueRelay` (ownership, and the server → clients send) is implemented by `AbilityNetworkMediator`,
      and forwarded by `HumanoidPlayer`, which is the controller the simulation passes.
    - `IGameplayCuePlayer` (play here now) is how the RPC reaches the use case.
  - **Dispatcher:** it is its own class, `GameplayCueDispatcher`.
  - **`GameplayCueNotify`:** it holds only its cue tag so far. The action lists come in step 3.
  - **On-object handlers:** they are found once per actor, anywhere under it but not inside a nested actor. Players
    register twice under one Id (`HumanoidPlayer` and its mediator), so the use case dedupes by Id.
  - **Profiles:** `GameplayCuesFeatureInstaller` (Order -19) is listed in `Profile_Base` and in `Profile_Test_Core`,
    which lists its installers itself.

## Tests and verification
- **EditMode:**
  - `GameplayCueStateTrackerTests`: edges, no-op when unchanged, several effects sharing one cue tag.
  - `GameplayCueDispatchProcessorTests`: the table above.
  - `GameplayCueCatalogTests`: contributions gathered; an installer that is absent contributes nothing.
  - `AbilitySystemUseCase` cue tests:
    - Instant fires Execute;
    - Duration/Infinite add and remove the cue tags;
    - `IsPredicted` only from `ProcessAbilitySimulation`;
    - no dispatcher is fine.
  - Action tests where they are pure (`HudToast` through a fake `IHudValues`).
- **Scenarios, extended rather than new.** Probes `CueCount <cue> <event> <n>` and `CueActive <cue>` on each peer.
  - `ShipDamage`: exactly one `Cue.Ship.Part.Break` Execute per peer. `Cue.Ship.Leak` is active while broken and
    removed after.
  - `RepairLoop`: `Cue.Player.Repairing` is active while repairing, including on the predicting owner.
    `Cue.Ship.Part.Broken` Removed happens once per peer.
  - Late join: a client joining after the break has `Cue.Ship.Part.Broken` and `Cue.Ship.Leak` active.
- `.\.tools\verify.ps1 -Scenario ShipDamage` and `-Scenario RepairLoop`, solo and host + client. The other scenarios
  stay green.
- **Human playtest:**
  - the break is audible and visible from across the deck;
  - the hiss reads as "something leaks";
  - the repair loop and the ding feel right;
  - a late joiner sees and hears a broken part.
- **Docs:**
  - `ARCHITECTURE.md`: the cue model (events versus cues; tag identity; state versus burst by duration type;
    feature-contributed notifies).
  - `CODE_MAP.md`: a feature-index row.
  - `TASK_GUIDES.md`: a recipe, "Add feedback to a gameplay moment".
  - Mark step 3b in `ship-damage-repair.md`.

## Decisions (developer, 2026-09-27: all recommended options)
- **A. Action authoring: a combination.** An action list in the notify asset (`[SerializeReference]
  List<GameplayCueAction>` plus a type-picker drawer, modelled on
  `Features/Interaction/Editor/HandlerTypeReferencePropertyDrawer.cs`). One of the actions is `SpawnPrefab` (pooled)
  for spatial or looping presentation.
- **B. Tag wire format (slice 0):** `NetworkList<FixedString64Bytes>` of tag names. This is independent of the
  optional tags installer and keeps the same identity as today.
- **C. Cue tags share the gameplay tag set.** Replication and prediction stay in one place. "Gameplay logic never
  reads `Cue.*`" is enforced by convention and review, and documented in `ARCHITECTURE.md`.
