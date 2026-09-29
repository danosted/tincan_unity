Status: Approved (phase 1 done)

# Feature assemblies: compiler-enforced boundaries

## Why
Feature installers contain a feature's registrations, but nothing contained its code. Everything under
`Assets/Scripts/Features/` compiled into one assembly, so any feature could use any other, core could depend on
features, and "core" versus "feature" was a convention that had already blurred: `Profile_Base` holds core systems
dressed up as switchable features.

Giving each feature its own asmdef makes the boundary a compile error instead of a review comment:
- a feature sees only what it references;
- core cannot see features;
- a reference between features doubles as that feature's profile requirement, so there is no hand-kept list.

## The rule
Every feature is its own assembly, `TinCan.Features.<Name>`, referencing only the core, the packages it uses, and
the features it builds on. The rule, diagram and enforcement are documented in `CODE_MAP.md` ("Assemblies and the
one-way rule"), with pointers from `AGENTS.md`, `ARCHITECTURE.md` §7, `CODE_STANDARDS.md`, `FEATURE_INSTALLERS.md`
and `TUTORIAL_NEW_FEATURE.md`.

Rule tests in `ArchitectureRulesTests`:
- `FeatureInstallers_LiveInTheirOwnAssembly`, which ratchets: legacy installers are listed in
  `ArchitectureRulesBaseline.InstallersInSharedAssemblies` and may only leave.
- `SharedAssemblies_DoNotReferenceFeatureAssemblies`.
- `FeatureProfiles_LoadTheFeaturesTheirFeaturesReference`.
- `AssemblyCSharp_HoldsOnlyTheTopLayer`.

## Phases
1. **Leaf features, and the pattern (done).** `GasChallenge` (the worked example) and `SkyHazards` (referenced by
   DevTools and tests) each got an asmdef. The docs and the tests above went in with them. A probe confirmed that
   GasChallenge cannot use a SkyHazards type without a reference (CS0234).
2. **More leaves, one at a time.** Each move:
   - add the asmdef;
   - add references in DevTools and tests;
   - remove the installer from the baseline;
   - re-pick any `IA_*` handler the move breaks (`InteractionDefinitions_ResolveTheirHandler` lists them);
   - update its feature index row.

   Done: `Stations` and `Weapons.Cannon`. Cannon references Stations, the first feature-to-feature reference, so
   every profile loading Cannon must load Stations; both cannon profiles already do. `IA_OccupyCannon` was re-pointed.
   DesignedEvents moved with phase 3, because ShipDamage references it.
3. **Split `Features/Airship/` (done).** The ship itself stays in the shared block: the root files and `Fixtures/`.
   Its features are now assemblies:
   - `Airship.Fuel`;
   - `Airship.Fuel.Minigame` (FlyingCan and net catch; references Fuel);
   - `Airship.Damage` (references `DesignedEvents`);
   - `Airship.PhysicalParts` (the door);
   - `DesignedEvents`.

   Two tangles had to go first:
   - **Events ↔ Damage.** `EventCatalog` defined ship damage's HullStress event, while ShipDamage implemented its
     handlers: a cycle. Features now contribute their events through `FeatureInstaller.IExtension<EventDefinition>`,
     and ship damage authors HullStress in `ShipDamageDesignedEvents`.
   - **Carry → Minigame.** `TakeNetInteractionHandler` lived in `Features/Carry/` in the shared block while it
     referenced the minigame. It moved into the minigame.

   Re-pointed interactions: `IA_PourFuel`, `IA_TakeJerryCan`, `IA_TakeNet`, `IA_ToggleDoor`.

   **Follow-ups found on the way:**
   - `Carry/NetRackNetworkMediator` sits on the fuel fixture prefab, and `Carry/NetSwingVisualView` on the shared
     player prefab. Both are net-minigame content that other prefabs carry. Moving them into the minigame needs a
     socket for fixture parts and player visuals, not a file move.
   - `FlyingCanNetworkMediator` stays in Assembly-CSharp's `Network/Infrastructure`, because it derives from
     `NetworkMediator`, which lives there. It moves when that base moves down (phase 6).
   - ShipDamage now requires the events feature in every profile. It used to register its event handlers "for when
     events are loaded"; the compile-time reference makes that a hard requirement. Both profiles that load ShipDamage
     already load Events.
4. **Untangle the core systems and give them assemblies.** The folder-level scan shows cycles, which assemblies
   forbid:
   - Abilities ↔ Airship
   - Abilities ↔ HumanoidMovement
   - Targeting → HumanoidMovement → Abilities → Targeting
   - Possession ↔ Airship and Possession ↔ FreeCamera
   - Interaction ↔ Abilities and Interaction ↔ Airship

   Move the shared contracts down into `Core.Domain`. `ActorAbilityGrantUseCase` (Abilities → Airship,
   HumanoidMovement) should detect actor kinds through `Core.Domain` contracts when GAS moves.
5. **Make core mandatory.** Core-system installers go in one core profile the scope always loads, and scene profiles
   only add features. Core services are then guaranteed: drop `TryResolve` for them, and move the ability-grant
   socket into the GAS core instead of `GameplayTagsFeatureInstaller`.
6. **Empty Assembly-CSharp.** Unity compiles every script outside an asmdef into Assembly-CSharp, which sees
   everything and which nothing can reference.
   - **Done early:** `Core/Infrastructure` became `TinCan.Core.Infrastructure`. The composition root
     (`ProjectLifetimeScope`) moved to `Assets/Scripts/App/`, with its GUID kept, so it stays on top.
     `ActorOrchestrator` is now unit-tested directly instead of through reflection. Assembly-CSharp went from 28
     runtime scripts to 18, then 17 when Visual Scripting was removed with its `GASVisualScriptingBridge`.
   - **Guard:** `AssemblyCSharp_HoldsOnlyTheTopLayer` allows only the folders in
     `ArchitectureRulesBaseline.AssemblyCSharpFolders`, so a script that forgets its asmdef fails the suite.
   - **Remaining, after phase 4:**
     - `Network/Infrastructure` becomes `TinCan.Network`, above the core systems it bridges.
     - `App/` and `UI/` become `TinCan.App`, the one assembly allowed to reference everything.
     - Then the folder list is empty, and so is Assembly-CSharp.
     - Third-party scripts without an asmdef may still land there, which is harmless; the rule only covers
       `Assets/Scripts`.

## Watch out when moving a type between assemblies
- Script GUIDs are unchanged, so prefab and scene references survive.
- `InteractionDefinition` stores its handler as an assembly-qualified type name, so moving a handler blanks the
  `IA_*` dropdown. `InteractionDefinitions_ResolveTheirHandler` catches it; re-pick the handler in the Editor.
- `[SerializeReference]` types need `[MovedFrom]`.
- Assembly-CSharp cannot be referenced by an asmdef: features reach `Network/Infrastructure` only through interfaces.
