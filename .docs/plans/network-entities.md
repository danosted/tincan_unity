Status: Done (2026-09-27)

# Network entities (slice 4 of `architecture-review.md`: A2, plus the legacy-interaction cleanup)

## Context
Verified at `1f3bdbf`. Direction agreed with the developer (2026-09-27): fix registration and identity for good, as
the first layer of a later session/lobby layer and persistent world state (a REST API shaped to fit the game).
GUIDs are the id format.

What is wrong today:
- **No stable identity.** `NetworkMediator.Id` and `FuelTankNetworkMediator.Id` are `Guid.NewGuid()` per component
  instance: the same ship has a different `Id` on every peer and after every respawn. Nothing can be persisted or
  resynced against it. `AbilityNetworkMediator` borrows its sibling's `Id` only if `HumanoidPlayer` comes first on
  the prefab.
- **Everyone registers themselves, five ways:** the `NetworkMediator` base (6 subclasses, so the player and the ship
  register their hierarchy twice), `FuelTankNetworkMediator` (its own `RegisterHierarchy`),
  `AirshipNetworkMediator` (also `Registry.Register(this)`), `ShipModuleNetworkMediator` (the ship's module registry),
  and a `FindObjectsByType<NetworkMediator>` injection scan in `ProjectLifetimeScope`. The first despawn unregisters
  everything while the second mediator is still live.
- **Possession leaks.** Every `NetworkMediator` is `IPossessable`, so `PossessionUseCase.SwitchToNext` offers flying
  cans, ship modules and ability mediators as candidates.
- **Dead code** (approved for removal): the legacy interaction path (`IInteractionRequester`,
  `RequestInteractionServerRpc`, `InteractivityUseCase`), `MaintenanceUseCase`, and the legacy `ShipRepairPoint` /
  `ShipDamagePoint` children on `Airship_Prefab` (`ShipRepairPointNetworkMediator`).

Facts the design rests on: no `NetworkObject` lives in a scene; all eight come from prefabs (`NetworkPlayer`,
`Airship_Prefab`, `FlyingJerryCan`, `Cannon_Module`, `FuelSystem`, `RepairToolRack`, `ShipDamageSockets`,
`PossessionMediator`). One `NetworkObject` can hold several actors (`ShipDamageSockets`: five GAS sockets).
NGO 2.11 has `OnNetworkPostSpawn`, called after every behaviour on the object has spawned.

## Design
1. **`EntityNetworkMediator`: one per `NetworkObject`, the only registrar.** `[DisallowMultipleComponent]`,
   `[RequireComponent(typeof(NetworkObject))]`, placed on the root of each networked prefab.
   - **`EntityId`** (`Core/Domain`, a GUID, network-serializable): a server-written `NetworkVariable`, so every peer
     has it in `OnNetworkSpawn`, late joiners included. The server assigns a new one unless a spawner preset it
     (`PresetEntityId` before `Spawn`: the hook a saved world will use to respawn with the same ids).
   - **Actor ids come from the entity.** An actor on the entity's root object has `Id == EntityId`. An actor on a child
     object gets a GUID derived from the `EntityId` and its path under the root (a name-based UUID). The result is
     identical on every peer and across respawns of the same entity. Two actor components on one object (player,
     ship) share the id by construction, whatever the component order.
   - **Registration once.** In `OnNetworkPostSpawn` it asks the orchestrator to register the entity: actors,
     interactors, one ability controller per actor (`AbilityControllerSelection`), targetables. In
     `OnNetworkDespawn` it unregisters them. Nothing else calls `RegisterHierarchy` or `Registry.Register`. Linking a
     fixture to its ship (`RegisterShipModule`) stays: it is a relationship, not registration.
   - An `IEntityRegistry` (entity id → entity, and the actor ids it owns) is the seam the session layer will own
     later.
2. **`NetworkMediator` becomes thin:** identity (reads the entity's id for itself), `IsSimulating`, injection. No
   possession, no interaction, no registration.
3. **Possession is a component.** `PossessableNetworkMediator` holds the replicated possessor and the
   player-object auto-possession. `HumanoidPlayer` and `AirshipNetworkMediator` (whose domain views extend
   `IPossessable`) implement it by delegating to that component, as `HumanoidPlayer` already delegates GAS. The
   other mediators stop being `IPossessable`. Added to `NetworkPlayer` and `Airship_Prefab` in the Editor.
4. **Dead code removed** as listed above, plus the `ProjectLifetimeScope` injection scan (no scene network objects).
5. **Rules** (added to `ArchitectureRulesTests`): every networked prefab has exactly one `EntityNetworkMediator`
   (asset scan); only `EntityNetworkMediator` calls `RegisterHierarchy` (source scan); no `Guid.NewGuid()` in a
   `NetworkBehaviour`.

Out of scope, next plans: the session/world layer (a session `LifetimeScope` owning the registries, lobby states,
`PlayerId` mapped to client ids, rejoin reclaiming a slot), and persistence (capture/restore of durable state per
entity, snapshots via the REST API). This slice leaves the seams for both: preset ids and the entity registry.

## Tests
- `EntityIdTests`: child ids are deterministic, differ per path, and match for the same entity id ("two peers").
- Registration (pure part): an entity with two actor components on its root registers one actor, one GAS
  controller; unregister removes all; a second register is a no-op.
- Possession: a non-possessable actor is not a `SwitchToNext` candidate.
- Rules baseline: `InteractivityUseCase` and `MaintenanceUseCase` leave the untested list; the scope limit drops.
- Scenarios: `.\.tools\verify.ps1 -All`. A new probe in `ShipDamageLateJoin` asserts the subject's actor id is the
  same on host and client (cross-peer identity).
- Human playtest: possession switching (player ↔ helm), free camera, late join.

## Docs
- `ARCHITECTURE.md` §4 (possession) and §5 (entities and registration, replacing "mediators register themselves").
- `CODE_MAP.md`: glossary entry for entity; trap about `HumanoidPlayer` component order removed.
- `TASK_GUIDES.md`: "add a networked prefab" now says: add `EntityNetworkMediator` to its root.
- Review: A2 and C14 done.

## Order of work
1. `EntityId`, id derivation, entity registry and the pure registration part, with tests.
2. `EntityNetworkMediator`; orchestrator registers per entity; remove the other registration paths.
3. Editor: add the component to the eight prefabs; check `NetworkPlayer` and `Airship_Prefab` diffs.
4. Possession component and delegation; slim `NetworkMediator`.
5. Dead code; rules; docs; `verify.ps1 -All`.

## Outcome (2026-09-27)
- EditMode 449/449: `EntityTests` (7) and two new rules (`NetworkedPrefabs_HaveExactlyOneEntity`,
  `OnlyEntitiesRegister_AndOnlyEntityIdsMakeIds`). Baseline shrank: `InteractorControllerView`'s `Camera.main`,
  `InteractivityUseCase` and `MaintenanceUseCase` gone; scope limit 49, files without `#nullable` 110.
- Scenarios, solo and host + client, all seven pass (9-28 s each). In `ShipDamageLateJoin` the host and the late
  client both report the subject as the same id and 7 identified entities (`EntitiesIdentified`).
- Changes from the design: the id struct is `NetworkEntityId` (Unity 6.4 has its own `UnityEngine.EntityId`);
  `FakeEntity` lives in `Tests/Shared` (MonoBehaviours in the Editor-only test assembly cannot be added); the actor
  registry still holds one primary actor per entity root, as before; the free camera also takes its id from
  `ActorIdentity`, so `EntityIds.New` is the only `Guid.NewGuid`. The legacy raycast in `InteractorControllerView`
  went with the legacy interaction path.
- Editor edits (saved, in the diff): `EntityNetworkMediator` on all eight networked prefabs;
  `PossessableNetworkMediator` on `NetworkPlayer` and `Airship_Prefab`; the `ShipRepairPoint` and `ShipDamagePoint`
  children removed from `Airship_Prefab`.
- Still for a human: possession switching (player ↔ helm, free camera) and a respawn, which no scenario covers.
