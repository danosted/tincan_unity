Status: Done (2026-09-27)

# Trust fixes (slice 3 of `architecture-review.md`: A3)

## Context
Verified at `ec080d1`.
- **Tag requests.** `AbilityNetworkMediator.RequestTagChangeServerRpc` lets an owner add or remove any tag on itself.
  Its only caller is `BuildModeUseCase`. `ClientTagState`'s optimistic adds and removes exist only for this RPC.
- **Placement.** `BuildPlacementNetworkMediator.RequestPlacementServerRpc(prefabName, …)` spawns any loaded
  `GameObject` found by name, unvalidated.
- **Build mode is an unfinished prototype.** Client placement never worked: the placement mediator is a separate,
  server-owned object, and the player has none, so a client "placement" calls `ModulePlacementUseCase` locally. The
  selection is a serialized prefab on `HumanoidPlayer`; the ghost strips components and raycasts `Camera.main` against a
  hard-coded layer. `GA_BuildMode` is also granted and bound to B, but is not toggleable and never ends.
- **Event trigger.** `CoordinatedEventMediator.TriggerEventRpc` has no caller; the mediator is on no prefab or scene.

## Decision (developer, 2026-09-27)
"Put build in a feature if easy, otherwise remove it for now." It is not easy (catalog, validation, an owned
requester, ghost and camera rework), so **build mode is removed**. A later feature can bring it back through an
installer, a buildables catalog and server validation.

## Design
1. **Remove build mode.**
   - Code: `BuildModeUseCase`, `BuildPlacementNetworkMediator`, `IBuildPlacementRequestor`, `IBuilder` (and
     `HumanoidPlayer`'s selection field), `ModulePlacementUseCase`/`IModulePlacementUseCase`, `GhostMaterialFeedback`,
     `BuildModeToggledEvent`, `BuildModeInput`, `ActionNames.BuildMode` and its key, `GASVisualScriptingBridge
     .IsPlacementValid`.
   - `ProjectLifetimeScope`: the placement-mediator spawn, `_buildingTag`, `_buildablePrefabs`, and two registrations.
   - Assets: `BuildPlacementMediator.prefab`, `GA_BuildMode`, `Input_ToggleBuildMode` (and its binding),
     `State.Building`. Editor edits: the entries in `DefaultNetworkPrefabs`, `NetworkPlayer`'s starting abilities, and
     `GhostMaterialFeedback` on `Cannon_Module`.
   - Kept: `ModuleSpawningService` (fixtures use it), `Cannon_Module` and the ship-module mediators.
2. **No client tag writes.** Remove `RequestTagChangeServerRpc`. On a client, `AddTag`/`RemoveTag` log an error and do
   nothing: only effects and the server change tags. Remove the optimistic layer from `ClientTagState`.
3. **Event trigger.** Delete `TriggerEventRpc`.

## Tests
- `ClientTagStateTests`: optimistic cases removed; replicated and predicted cases kept.
- Rules baseline: `BuildModeUseCase` and `ModulePlacementUseCase` leave the untested list; the registration limit
  drops.
- Scenario `TagRequest` is removed (it tested the removed RPC). The seven others stay green, solo and host + client.

## Docs
- `ARCHITECTURE.md` §3: clients never write tags. `CODE_MAP.md`: remove the build-mode row. Review: mark A3 done.

## Outcome (2026-09-27)
- EditMode 440/440. The rules baseline shrank: `BuildModeUseCase` and `ModulePlacementUseCase` left the untested
  list, the scope limit is 50 (was 52), and the files without `#nullable enable` are down to 115 (was 120).
- Scenarios, solo and host + client, all seven pass on the first run.
- Editor edits (made through the Editor, saved, visible in the diff): `NetworkPlayer` (starting abilities, build
  selection), `Cannon_Module` (ghost component), `DefaultNetworkPrefabs`, `DefaultInputBindingConfig`,
  `GameLifetimeScope` (stale fields dropped), `GameplayTagDatabase` (updated itself when `State.Building` went).
- B no longer does anything.
