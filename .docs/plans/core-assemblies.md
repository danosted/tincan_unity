Status: Done

# Core systems into their own assemblies (phase 4 of feature-assemblies.md)

## Why
After phases 1–3 the shared `TinCan.Features` block holds about 190 files: the core systems plus a few leftovers.
Everything in it can still use everything else, so "core" has no internal shape. Phase 5 (a mandatory core
profile, no `TryResolve` for core services) needs the core systems to be real assemblies with a one-way order.

## What the scan found
A type-level scan of the shared block, ignoring comments and strings, found far less coupling than the folder-level
scan suggested. Most of that earlier coupling was doc comments and feature code that has since moved out. Every
cycle hangs on one to four types:

| Cycle | Caused by |
|---|---|
| Abilities ↔ HumanoidMovement | `AbilitySystemUseCase` takes a `HumanoidInputState`; `ActorAbilityGrantUseCase` checks `IHumanoidCharacterView`. The other way: `HumanoidAttributeSet` lives in Abilities. |
| Abilities → Targeting → HumanoidMovement → Abilities | `HumanoidTargeter` lives in Targeting. |
| Abilities → UI | `GameplayCuePresenter` uses `IHudValues` (HUD toast cues). |
| FreeCamera ↔ HumanoidMovement ↔ Ship | Camera contracts split across folders: `IOrbitalLookView` (Humanoid) and `IHasOrbitalCamera` (FreeCamera). |
| FreeCamera ↔ Possession, Ship ↔ Possession | `IPossessionReceiver` and `IHasOrbitalCamera` used both ways. |
| HumanoidMovement → Ship | `ParentLocalSpaceVolume` (the moving-ground volume) lives in the ship folder. |
| Possession → Ship → Interaction → Possession | `ServerPossessionManager` resets `AirshipInputState` on a possession change. |
| Ship → ThirdPersonCharacter → HumanoidMovement | `ThirdPersonLookView` has its own folder (CODE_MAP already says it belongs with the humanoid). |

## Target layering (lowest first; each references only layers below)
1. `TinCan.Core.Domain`: contracts. It gains the shared camera and possession contracts (`IOrbitalLookView`,
   `IHasOrbitalCamera`, `IPossessionReceiver`), `IHudValues`, and the actor-kind contract for ability grants.
2. `TinCan.Targeting`.
3. `TinCan.Gas`: abilities, effects, tags, cues, and the ability-grant socket.
4. `TinCan.Possession`.
5. `TinCan.Humanoid`: movement, input state, attribute set, `HumanoidTargeter`, `ThirdPersonLookView`,
   `ParentLocalSpaceVolume`.
6. `TinCan.Interaction`.
7. `TinCan.Ship`: the airship root and `Fixtures/`.
8. `TinCan.Items`, `TinCan.UI`.
9. `TinCan.Entities` (`EntityNetworkMediator`): core, referenced by every networked prefab. It is placed where the
   scan puts it.

Features carved out of the block at the same time: `CloudBoundary` (with submersion), `FreeCamera`, `Environment`,
and `Events` (`ToggleShipTagStation`, parked with the booster). The two `Carry` leftovers stay until the socket
follow-up from phase 3.

## Steps
1. **Contracts down, no new assemblies yet.**
   - Move the contracts above into `Core.Domain`.
   - Move `HumanoidTargeter`, `HumanoidAttributeSet`, `ParentLocalSpaceVolume` and `ThirdPersonLookView` to the
     humanoid.
   - `AbilitySystemUseCase` takes the input mask instead of `HumanoidInputState`.
   - The ship resets its own input when its possessor changes, instead of `ServerPossessionManager` doing it.
   - Actor kinds for ability grants come from a `Core.Domain` contract instead of `IHumanoidCharacterView` and
     `IAirshipView`.
   - Scripts move with the Editor so GUIDs stay. Compile and test after each move.
2. **Re-scan: zero cycles** before any asmdef is added.
3. **One asmdef per system, bottom-up** (Targeting first, UI last), each with compile and tests green. On each move:
   - the DevTools and test references;
   - `InteractionDefinitions_ResolveTheirHandler` for handlers that moved;
   - a new `ArchitectureRulesBaseline` list of core assemblies, so `SharedAssemblies_DoNotReferenceFeatureAssemblies`
     also covers them.
4. **`[SerializeReference]` migration for GAS.** `GameplayCueNotify` actions are stored with their assembly name in
   every `GCN_*` asset. Give each action type `[MovedFrom(sourceAssembly: "TinCan.Features")]` before moving, and add
   a rule test that every `GCN_*` asset still loads all its actions.
5. **Carve the four leftover features** into feature assemblies, following the GasChallenge example.
6. **Docs**: CODE_MAP layering and table, ARCHITECTURE §7, the feature-assemblies roadmap.
7. **One confirmed full scenario run** at the end.

`TinCan.Features` ends empty apart from `Carry`, and is deleted when that follow-up lands.

## Decisions (taken 2026-09-29: D1 one per system, D2 Core.Domain, D3 features)
- **D1. Granularity.** One assembly per system as above (recommended: the cycles are few and named), or fewer,
  grouped ones (for example humanoid, ship, possession and camera as one "actors" assembly: fewer moves, less
  isolation).
- **D2. Where shared contracts live.** `Core.Domain` (recommended: it already holds every other cross-system
  contract), or small per-system contract assemblies.
- **D3. FreeCamera and CloudBoundary: feature or core?** Both are installers in `Profile_Base` today. Recommended:
  features, since a scene can sensibly run without them, which matters for phase 5's mandatory core.

## Risks
- Namespaces follow folders, so moved types change namespace:
  - handler names in `IA_*` assets break (the rule test catches them);
  - `[SerializeReference]` data breaks (step 4);
  - `m_EditorClassIdentifier` strings in prefabs change, which is cosmetic.
- `NetworkBehaviour`s keep their GUIDs, and NGO prefab hashes do not depend on assembly. `verify.ps1` compares host
  and clone hashes anyway.

## Outcome
- **Steps 1–2:** the contracts moved as planned; the camera contracts landed in `Core/Domain/Look/`, because a
  `Camera` namespace hid Unity's `Camera` class. A type-level re-scan and a topological sort found zero cycles.
  Along the way:
  - GAS's `ProcessAbilitySimulation` now takes the input mask;
  - the airship clears its own input when released;
  - grants detect actors by the `IHumanoidActor` / `IShipActor` markers.
- **Step 3:** the asmdefs were generated from a type-usage scan and compiled first time after removing stale
  `using` lines. A name clash (`ActorAbilityGrantUseCase.Grant` against Items' private `Grant` class) would have
  added a false Gas → Items cycle; the method is now `GrantTo`.
- **Step 4:** the `[MovedFrom]` attributes work: `GameplayCueNotifies_LoadAllTheirActions` passes with GAS in its own
  assembly.
- **Step 5:** all four features were carved. `InstallersInSharedAssemblies` is empty, and
  `SharedAssemblies_DoNotReferenceFeatureAssemblies` now covers every core assembly.
- **Re-pointed interactions:** `IA_AcquireAirshipControl`, `IA_DamageModule`, `IA_RepairModule`, `IA_TakeRepairTool`,
  `IA_ToggleFlightBoost`.
- **Left for later:**
  - `Carry/` stays in the shared block until the socket follow-up.
  - Done later the same day: the core systems moved to `Assets/Scripts/Core/<System>/` with namespaces
    `TinCan.Core.<System>` (the door, which shared the ship's namespace, became `TinCan.Features.Airship.PhysicalParts`).
