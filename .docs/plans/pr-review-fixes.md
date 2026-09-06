Status: Approved

# Stacked PR review fixes

The developer requested fixes and commits on the branches where the findings originated.

- PR #16: identify catches by the active swing effect, and keep catch geometry independent of Unity views. Add regression coverage for consecutive swings on the same tick.
- PR #17: delegate fuel fixture actor and module lifecycle registration to the actor orchestrator; cover attachment, reparenting and cleanup.
- PR #18: correct the explanation of airship GAS ticking and align the feature recipe with orchestrated module registration.
- PR #19: carry forward the preceding fixes; no independent finding.

Validate each code change with Unity compilation and EditMode tests. Preserve the stack with additive commits and merges, without rewriting branch history.

## PR #16 completed

`Assets/Scripts/Features/Abilities/AbilitySystemUseCase.cs` exposes the latest live effect granting a tag. `Assets/Scripts/Features/Airship/Fuel/Minigame/NetCatchUseCase.cs` uses that instance to enforce one catch per swing, even when expiration and reactivation happen in the same simulation step. It snapshots Unity positions before calling the value-only geometry in `Assets/Scripts/Features/Airship/Fuel/Minigame/CatchProcessor.cs`.

The consecutive-swing regression failed with one catch before the fix and passes with two afterward. Unity 6000.4.5f1 compilation completed without errors; all 148 EditMode tests passed. Human playtest remaining: repeated net swings on host and client, including two players reaching the same can.

## PR #17 completed

`Assets/Scripts/Features/Airship/Fuel/FuelTankNetworkMediator.cs` delegates its actor/capability lifecycle and ship membership to `Assets/Scripts/Core/Infrastructure/ActorOrchestrator.cs` through `Assets/Scripts/Core/Domain/IActorOrchestrator.cs`. The orchestrator keeps ship membership idempotent and removes previous membership on reparenting; the tank rebinds its attribute view to the current ship. The fixture recipe in `.docs/FEATURE_INSTALLERS.md` describes this ownership.

`Assets/Tests/EditMode/FuelFixtureRegistrationTests.cs` covers actor/capability registration, early and late parenting, duplicate server attachment, reparenting, detachment and despawn. Unity compilation completed without errors; all 159 EditMode tests passed. Human playtest remaining: host/client fixture spawn and a late-joining client's fuel gauge.
