Status: Done

# Feature containment: sockets and a service check

## Why
A profile can leave any feature out, but the player and the airship spawn in every scene. A left-out feature could
fail in three ways:
- **Crash:** a shared-prefab component injects a feature service, so VContainer throws at spawn.
- **Half-work:** an ability is granted whose use case isn't loaded, or an interaction's handler isn't registered and
  the interaction silently does nothing.
- **Missing dependency:** a feature needs another feature's services, and that feature isn't loaded.

The goal: a left-out feature is absent, never half-present, and a profile that can't work doesn't start.

## What changed
- **Convention** in `FEATURE_INSTALLERS.md` ("Features and shared prefabs"), with pointers from `ARCHITECTURE.md` §7,
  `CODE_MAP.md`, `TASK_GUIDES.md` and `TUTORIAL_NEW_FEATURE.md`.
- **Ability socket:** installers contribute `ActorAbilityGrant`s (`Features/Abilities/`) through
  `FeatureInstaller.IExtension<ActorAbilityGrant>`.
  - `ActorAbilityGrantUseCase` grants them on every peer when the actor registers, using the new
    `IActorRegistry.OnActorRegistered`.
  - `HumanoidPlayer` and `AirshipNetworkMediator` are unchanged: they know nothing about features.
  - The use case is registered by `GameplayTagsFeatureInstaller`, which every profile loads.
- **Service check:** `InstallerServiceCheck` (`Core/Domain/Features/`) records each installer's registrations. It
  checks that every constructor and `[Inject]` dependency is registered.
  - `ProjectLifetimeScope` throws a list of the gaps, so the game doesn't start.
  - `IObjectResolver` (optional `TryResolve`) and collections never count as missing.
  - Instances and factories are not analysed.
  - This replaces a hand-kept `Requires` list, which was removed.
- **`InteractionOrchestrator`** warns once per handler type when no loaded installer registers a target's handler.
- **Tests:**
  - Rules in `ArchitectureRulesTests`: `SharedPrefabs_DoNotRequireFeatureServices` (baseline
    `SharedPrefabFeatureDependencies`, empty today) and `FeatureProfiles_CanBuildTheirServices`.
  - Unit tests: `ActorAbilityGrantUseCaseTests`, `InstallerServiceCheckTests`, `InteractionOrchestratorTests`.

## Findings
- No shared-prefab component injects a feature service today. A temporary hard `[Inject]` of `ITargetingService` on
  `HumanoidPlayer` confirmed the rule catches one.
- Every profile passes the service check. `Profile_Test_Core` without Targeting is caught:
  "InteractionFeatureInstaller: InteractInputUseCase needs ITargetingService".
- Not caught: dependencies that aren't services, such as FlyingCan's need for Fuel's jerry cans.
- `GA_RepairModule` on `NetworkPlayer.prefab` stays. It belongs to legacy build mode, which is core.
- No feature grants through the socket yet. The next feature ability that every player needs should be the first.
