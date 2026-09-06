# Tutorial: build a feature the way Fuel was built

This walks through a made-up feature, **Ballast**: a water tank fixture on the ship that slowly fills while the
ship is driven, a valve you press E on to dump it, and a HUD readout. It maps one-to-one onto the Fuel feature, so
every step names the real Fuel file to open and copy. When the tutorial and the code disagree, the code wins;
fix the tutorial.

Read [`CODE_MAP.md`](CODE_MAP.md) once first. Reference docs: [`FEATURE_INSTALLERS.md`](FEATURE_INSTALLERS.md),
[`ARCHITECTURE.md`](ARCHITECTURE.md), [`CODE_STANDARDS.md`](CODE_STANDARDS.md).

## Before you start: five questions

Answer these; they decide which steps below you need.

1. **Who owns the state?** Almost always the server. Clients read replicated values and predict from input.
2. **Per tick or per frame?** Logic that must agree across peers runs on the fixed tick (`ISimulationTickable`).
   Visuals and HUD run per frame (`ITickable`, `MonoBehaviour.Update`).
3. **Where does it live in the world?** On the ship (a fixture, spawned by the installer), on the player (a child
   of `NetworkPlayer.prefab`, a shared file), or nowhere (a global system).
4. **How does the player trigger it?** Press E (interaction handler) or a key that must be predicted (an ability
   input bit). Never a bare `ServerRpc` for simulated actions.
5. **Which numbers will someone tune?** They go in a `*Config` ScriptableObject.

Ballast: server-owned; fill per tick, HUD per frame; on the ship; press E; capacity and fill rate.

## Do NOT touch

These are shared files. A feature built on the installer pattern never edits them:

- `Assets/Scripts/Core/Infrastructure/ProjectLifetimeScope.cs`
- `Assets/Scripts/Network/Infrastructure/NetworkSimulationScheduler.cs`
- `Assets/Prefabs/Singletons/GameLifetimeScope.prefab`
- `Assets/Prefabs/Airship/Airship_Prefab.prefab`
- `Assets/DefaultNetworkPrefabs.asset`
- `Assets/Prefabs/NetworkPlayer.prefab` (unless the feature genuinely lives on the player; then one child only)
- `Assets/Scripts/Core/Domain/` (unless you are adding a contract several features share)
- `Assets/Abilities/Inputs/DefaultInputBindingConfig.asset` (unless you add a new predicted input)

## Build order

Steps 1 to 16 are C# only and can be done without the Editor open. Steps 17 to 21 are authored in the Editor.
Recompile after every few files:

```bash
unity cmd recompile && unity cmd recompile_status
```

### 1. Create the folder

`Assets/Scripts/Features/Ballast/`. It compiles into `TinCan.Features` automatically; no asmdef needed. Every
file starts with `#nullable enable`.

### 2. Config

`BallastConfig.cs` from `Assets/Scripts/Features/Airship/Fuel/FuelConfig.cs`. Keep the shape: `[CreateAssetMenu(fileName = "BallastConfig", menuName = "TinCan/Airship/Ballast Config")]`, `[Header]` groups, `[Tooltip]` and `[Min]` on every field. Ballast needs `Capacity`, `FillPerSecondAtFullThrottle`, `DumpLitres`.

### 3. Domain interfaces

`IBallastTank.cs` from `Fuel/IFuelTank.cs`: `Level`, `Capacity`, `IsFull`, `Config`, `Fill(float)`,
`float Dump(float)` (returns the amount actually removed). Add `IBallastValve { IBallastTank? Tank; }` in the
same file, like `IFuelFillPort`. Interfaces are what the tests and the handlers see; nothing else in the feature
references the mediator type directly.

### 4. Processor (pure math)

`BallastProcessor.cs` from `Fuel/FuelConsumptionProcessor.cs`: `ComputeFill(throttle, ratePerSecond, dt)`,
`ClampLevel(level, capacity)`, `IsDriven(hasPossessor, throttle)`. No state, no Unity types beyond `Mathf`.

**Checkpoint:** recompile clean.

### 5. Processor tests

`Assets/Tests/EditMode/BallastProcessorTests.cs` from `FuelConsumptionProcessorTests.cs`. Zero throttle fills
nothing; full throttle fills rate times dt; clamp holds at capacity.

**Checkpoint:**
```bash
unity cmd run_tests --mode EditMode
```

### 6. Events

`BallastEvents.cs` from `Fuel/FuelEvents.cs`: `readonly struct BallastFullEvent(Guid AirshipId)`,
`BallastDumpedEvent(Guid RequesterId, float Removed)`, each with `ToString()`. Published through
`IEventPublisher`; `DebugLogEventObserver` prints them.

### 7. (Optional) GAS attribute

Only if other systems should read the value as an attribute or modify it with effects. Fuel does this:
`Assets/Abilities/Attributes/Attr_Fuel.asset` plus `Fuel/FuelAttributeSet.cs`, storing the level in `BaseValue`
because effects reset `CurrentValue`. Ballast can skip it and keep the level in a `NetworkVariable<float>`
instead (see `JerryCanSupplyNetworkMediator.cs` for the `NetworkVariable` + `OnValueChanged` shape).

### 8. Network mediator (the fixture root)

`BallastTankNetworkMediator.cs` from `Fuel/FuelTankNetworkMediator.cs`:
`NetworkBehaviour, IBallastTank, IShipModule`. Keep these parts exactly:
- `[SerializeField] BallastConfig? _config` (the fixture-scoped config style).
- Inject `IActorOrchestrator`; call `RegisterHierarchy(gameObject)` in `OnNetworkSpawn` and
  `UnregisterHierarchy(gameObject)` in `OnNetworkDespawn` for actor and capability membership.
- Bind to the parent ship in **both** `OnNetworkSpawn` and `OnNetworkObjectParentChanged`; also accept the
  server's `IShipModule.OnAttachedToShip` callback. Delegate membership to the orchestrator's
  `RegisterShipModule(this, registry)` and `UnregisterShipModule(this)` methods. Rebind local state when
  changing ships, and clear the binding on detachment or despawn. See `Fuel/FuelTankNetworkMediator.cs`
  and `Assets/Tests/EditMode/FuelFixtureRegistrationTests.cs` for early/late parenting and repeated attachment.
- Every write (`Fill`, `Dump`) starts with `if (!IsServer ...) return;` and clamps through the processor.

### 9. Locator

`BallastTankLocator.cs` from `Fuel/FuelTankLocator.cs`: static `Find(IAirshipView)`, `FindFixture<T>`,
`IsAlive`. Use `(airship as Component)?.GetComponentInChildren<T>(true)` first; `IAirshipView.Transform` can
already be destroyed during teardown.

### 10. Use case (fixed tick)

`BallastFillUseCase.cs` from `Fuel/FuelConsumptionUseCase.cs`: `ISimulationTickable`, `Phase => SimulationPhase.AfterAirship`,
constructor `(INetworkService, IActorRegistry, ITimeService, IEventPublisher, BallastProcessor)`. First line of
`Tick()`: `if (!_networkService.IsServer) return;`. Cache tanks in `Dictionary<Guid, IBallastTank>` and
revalidate with `IsAlive`. Publish `BallastFullEvent` once on the transition, not every tick (Fuel uses a
`HashSet<Guid>` for one-shot state).

### 11. Use case tests + fake

`Assets/Tests/EditMode/Fakes/FakeBallastTank.cs` from `Fakes/FakeFuelTank.cs`: a plain fake recording
`TotalFilled` / `DumpCalls`, plus a `FakeBallastTankBehaviour : MonoBehaviour` with `AttachTo(GameObject, config)`
so the locator path is exercised. `BallastFillUseCaseTests.cs` from `FuelConsumptionUseCaseTests.cs`: no work
on client; no fill without possessor; fills when driven; finds the tank on a child object; full event exactly once.

**Checkpoint:** tests green.

### 12. Interaction handler

`DumpBallastInteractionHandler.cs` from `Fuel/PourFuelInteractionHandler.cs`: `IInteractionHandler`,
constructor `(IEventPublisher)`, `Handle` starts with `if (context.Target is not IBallastValve valve) return;`,
then a switch over the state, `LogInfo(LogSource, ...)` on every refused branch, `BallastDumpedEvent` on success.
No `IsServer` check here: the orchestrator only runs handlers on the server, and the mediator guards the write.

### 13. Handler tests

`DumpBallastInteractionHandlerTests.cs` from `PourFuelInteractionHandlerTests.cs`: empty tank is a no-op; dump
removes `DumpLitres`; event carries the removed amount. Use the same `RecordingPublisher` and
`Context(requester, target)` helpers.

### 14. Interaction target

`BallastValveNetworkMediator.cs` from `Fuel/MotorFillPortNetworkMediator.cs`: `NetworkBehaviour, IInteractionTarget, IBallastValve`,
one `[SerializeField] InteractionDefinition? _interactionDefinition`, `Tank` resolved with
`GetComponentInParent<IBallastTank>()` and cached behind `IsAlive`.

### 15. HUD presenter (per frame)

`BallastHudPresenter.cs` from `Fuel/FuelHudPresenter.cs`: `ITickable`, `IHudValues.Set("Ballast", ...)`,
`Remove` when no ship. Test from `FuelHudPresenterTests.cs` with `Fakes/FakeHudValues.cs`. Optional in-world
gauge: `Fuel/FuelGaugeView.cs` with its static `NeedleAngle` and `FuelGaugeViewTests.cs`.

### 16. Installer class

`BallastFeatureInstaller.cs` from `Fuel/FuelFeatureInstaller.cs`. This is the one place registration lines live:

```csharp
[CreateAssetMenu(fileName = "BallastFeatureInstaller", menuName = "TinCan/Features/Ballast Feature Installer")]
public class BallastFeatureInstaller : FeatureInstaller
{
    [SerializeField] private ShipFixtureDefinition? _ballastSystemFixture;

    public override void Install(IContainerBuilder builder)
    {
        builder.Register<BallastProcessor>(Lifetime.Transient);
        builder.Register<BallastFillUseCase>(Lifetime.Singleton).AsSelf().As<ISimulationTickable>();
        builder.Register<DumpBallastInteractionHandler>(Lifetime.Singleton).As<IInteractionHandler>();
        builder.Register<BallastHudPresenter>(Lifetime.Singleton).As<ITickable>();
    }

    public override IEnumerable<GameObject> NetworkedPrefabs
    {
        get { if (_ballastSystemFixture != null && _ballastSystemFixture.Prefab != null) yield return _ballastSystemFixture.Prefab; }
    }

    public override IEnumerable<ShipFixtureDefinition> ShipFixtures
    {
        get { if (_ballastSystemFixture != null) yield return _ballastSystemFixture; }
    }
}
```

Override `Order` only if you depend on another feature's registrations (`FlyingCanFeatureInstaller` is 10
because it needs Fuel). If a reference is missing, degrade to "feature off" with a warning, as
`FlyingCanFeatureInstaller` does with its config.

**Checkpoint:** recompile clean, all tests green, including `FeatureCompositionTests`.

### 17. Fixture prefab (Editor)

`Assets/Prefabs/Airship/Parts/BallastSystem.prefab`, modelled on `FuelSystem.prefab`:
- Root: `NetworkObject` with `AutoObjectParentSync` and `SyncOwnerTransformWhenParented` on, **no**
  `NetworkTransform`; `BallastTankNetworkMediator` with the config assigned.
- Child `BallastValve`: mesh + collider at chest height (the interaction ray starts 1.5 m above the player
  pivot, 3 m range; short props need a tall `InteractVolume` trigger child), `BallastValveNetworkMediator`.
- Everything authored in ship-local coordinates. Measured deck heights in ship space: bow deck y = -1.82,
  mid deck -3.49, cabin floor -3.88, aft top deck 0.98.

### 18. Config asset (Editor)

**Assets > Create > TinCan > Airship > Ballast Config**, save as `Assets/Settings/BallastConfig.asset`, assign it
on the prefab's mediator.

### 19. Interaction definition (Editor)

**Assets > Create > TinCan > Interactions > Interaction Definition**, save as
`Assets/Interactions/IA_DumpBallast.asset`, pick `DumpBallastInteractionHandler` in the dropdown, assign it on
`BallastValve`.

### 20. Fixture definition (Editor)

**Assets > Create > TinCan > Features > Ship Fixture**, save as `Assets/Settings/Fixtures/BallastSystemFixture.asset`,
point `Prefab` at `BallastSystem.prefab`, leave the pose at zero if the prefab is already in ship space.

### 21. Installer asset (Editor)

**Assets > Create > TinCan > Features > Ballast Feature Installer**, save as
`Assets/Resources/Installers/BallastFeatureInstaller.asset`, assign the fixture. This is the moment the feature
turns on. Any `Resources/Installers` folder works, but keep them together.

### 22. Play

Play, Start Host. Expect: `BallastSystem` appears under the airship in the hierarchy; `Ballast: 0` on the HUD;
drive from the helm and the number rises; walk to the valve, E, number drops and a `BallastDumpedEvent` line is
in the console. Then Multiplayer Play Mode, one virtual player, Join `127.0.0.1`: same numbers on both peers.

```bash
unity cmd console --level error
```
must be empty.

### 23. Bookkeeping

Add a row to the feature index in [`CODE_MAP.md`](CODE_MAP.md#feature-index). Branch `<initials>/ballast`,
rebase on `origin/main`, PR to `danosted/tincan_unity` main with the file list and the playtest you did.

## Done checklist

- [ ] Every new `.cs` starts with `#nullable enable`; every `NetworkBehaviour` ends in `NetworkMediator`.
- [ ] Processor, use case and handler each have a `*Tests.cs`; all EditMode tests green.
- [ ] Every server write is behind `IsServer`; clients only read.
- [ ] No file in the "Do NOT touch" list changed (`git diff --stat main` to confirm).
- [ ] Installer asset exists under `Assets/Resources/Installers/` and all its references are assigned.
- [ ] Verified on host and one virtual client, including a late join.
- [ ] Feature index row added.

## Common failures

| Symptom | Cause |
|---|---|
| Feature does nothing, no errors | Installer asset is not under a `Resources/Installers` folder, or a reference on it is unassigned. Check the console for the installer's warning. |
| Pressing E does nothing | `IA_` dropdown is blank (handler renamed after the asset was made), or the collider is below the interaction ray, or `_interactionDefinition` is unassigned on the target. |
| Fixture spawns at the world origin or drifts | Prefab root is missing `AutoObjectParentSync`, or it has a `NetworkTransform`. |
| Test file will not compile: type not found | Your class landed in `Assembly-CSharp` (`Core/Infrastructure`, `Network/Infrastructure`, `Scripts/UI`). Move it to the feature folder. |
| Nothing moves in play, scheduler never resolves | A class has a test-only constructor overload and VContainer picked it. Mark the real constructor `[Inject]`. |
| Value correct on host, wrong on a late joiner | You relied on `NetworkVariable.OnValueChanged` for the initial value. Read `.Value` in `OnNetworkSpawn` as well. |
| Level resets to base when another effect fires | You stored a persistent value in a GAS `CurrentValue`. Use `BaseValue` (see `FuelAttributeSet`). |
| Works on host, tank never found on a client | Mediator binds only in `OnNetworkSpawn`. Also bind in `OnNetworkObjectParentChanged`. |
