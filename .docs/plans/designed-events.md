Status: Draft (parked 2026-09-28)

# Designed events (new feature; replaces the deleted coordinated events)

## Parked (read this first when picking it up)
Developer decision, 2026-09-28: keep the POC as it is ("it works-ish") and park the rest. Nothing below the POC
section is built.
- **What exists:** the POC (section "POC"): code-built `HullStress`, server-side director with auto-start, ship-damage
  handlers, scenario `HullStressEvent` green solo and host + client. Installer in `Profile_FuelSandbox` and
  `Profile_Test_ShipDamage`, so the sandbox auto-starts it 30 s after hosting.
- **Known POC limits:** event state is not replicated (the HUD announcement is host-only; clients only see broken
  parts); no schedule (the director rotates the whole catalog); linear phases only; static catalog.
- **Open before more work:**
  1. **What is a session?** (a voyage from A to B, survive N minutes, deliveries, ...). It decides how events trigger:
     pacing after a quiet gap, place (regions on a route), voyage beats, or consequences of player state. Without a
     session loop a director mostly relabels random breakage.
  2. **Scope.** Claude's assessment: the full design below (E1a–E1d: DI recipes, staged builder, branching,
     variation) pays off at dozens of events, not the three or four the current vocabulary allows. Suggested next
     step when resumed: rename, replicate the state (E1b), hand-write two or three more events on the POC API,
     playtest, then decide on the rest.
  3. **Name.** "Event" collides with the event bus (`IEventPublisher`, `*Event` structs), C# events and UnityEvents.
     Candidates: **Incident** (recommended: `IncidentDirector`, `IncidentDefinition`), Situation, Encounter.
     Taken: Challenge (gas challenge), Scenario (test harness).

## Goal
Everything that "happens" to the crew is a designed event: a definition with phases, actions and win/lose
conditions, started by a director, replicated as state so late joiners see it. Occurrences that today run on their own
random timers (ship breakage, flying-can waves) move onto events. Later events spawn targets and need a gunner.

## Today (verified)
- `ShipBreakageUseCase` breaks a random part on its own timer (`AutoBreak`). Both it and `FlyingCanUseCase` are server
  `AfterAirship` tickables. Correction (2026-09-28): flying cans are no longer waves on a timer; `FlyingCanUseCase`
  keeps a continuous field of cans ahead of the ship (`FlyingCanConfig.Enabled`, `MaxAlive`, rows). A
  `SpawnFlyingCanWave` action needs a new on-demand API there first.
- Scenarios are already authored as code with a fluent builder (`DevTools/Scenarios/Scenario.cs`, `Scenario.Builder`;
  catalog in `ScenarioCatalog.cs`), reviewed with the feature. Events copy that shape, but with typed steps instead of
  string command names.
- Handler lookup by type from DI exists for interactions (`Features/Interaction/InteractionHandlerRegistry.cs`).
- `GameplayTicks.FromSeconds` (`Features/Abilities/GameplayTicks.cs`) converts authored seconds to ticks.
- Entities (slice 4) give every networked object a stable id, so event state can reference spawned objects.
- No gunner or target mechanics exist. `Cannon_Module` is only a repairable ship module.
- A Unity Behavior node graph was tried as the authoring layer and rejected (`spike-unity-behavior-events.md`):
  frame time instead of ticks, no replicated state, assets that cannot be reviewed in git.
- A POC exists (see "POC" below): static catalog, linear phases, server-local state. The design below replaces its
  authoring API.

## Design

### Authoring: recipes, a builder factory, typed stages
Target shape (names are part of the proposal):

``` Csharp
public sealed class HullStressRecipe : IEventRecipe
{
    public EventId Id => EventIds.HullStress;

    public EventDefinition Build(IEventBuilder e)
    {
        var groan = e.DeclarePhase("Groan");
        var repair = e.DeclarePhase("Repair");
        var holds = e.DeclarePhase("Holds");

        return e.Named("Hull stress")
            .Describe("Parts break; the crew repairs them before the hull gives way.")
            .Phase(groan, p => p
                .Enter(a => a.Announce("The frame is groaning."))
                .After(Seconds(3), go => go.To(repair)))
            .Phase(repair, p => p
                .Enter(a => a.Ship().BreakParts(Range(1, 3)).Announce("Repair the breaks!"))
                .When(c => c.Ship().BrokenAtMost(0), go => go.To(holds))
                .When(c => c.Fuel().Below(10), go => go.Fail())
                .After(Seconds(60, 90), go => go.Fail()))
            .Phase(holds, p => p
                .Enter(a => a.Announce("The ship holds."))
                .After(Seconds(2), go => go.Succeed()))
            .OnFailure(a => a.Announce("The hull gave way."))
            .Build();
    }
}
```

- **No static construction (decision G).** An event is an `IEventRecipe` class. Installers register recipes
  (`builder.RegisterEventRecipe<HullStressRecipe>()`, an extension in the events feature). An injected
  `IEventBuilderFactory` hands each recipe its `IEventBuilder`; `EventCatalogService` (`IInitializable`) builds and
  validates every recipe once when the container is built and serves definitions by `EventId`. `EventId` is a
  typed struct; ids are listed in one `EventIds` class so uniqueness is visible in review. Both peers load the same
  profile, so both build the same catalog.
- **Typed stages (decision H).** Each builder call returns an interface that only offers the legal next calls.
  `Phase(handle, Func<IPhaseBuilder, IPhaseComplete>)`: a phase compiles only when it ends in an exit that always
  fires (`After(...)` or an unconditional `Then(...)`), so "a phase that can wait forever" is a compile error, not a
  runtime check. `When(...)` exits can be added before it, in order.
- **Typed vocabulary through scopes.** Actions are written in `Enter(a => ...)` against an `IActionScope`, conditions in
  `When(c => ...)` against an `IConditionScope`. Features add entry points as extension methods in their own folder
  (`a.Ship()`, `c.Ship()`, `c.Fuel()`), which return typed step builders (`BreakParts`, `BrokenAtMost`, `Below`).
  The events core never names another feature's types; the compiler checks every step and argument. Conditions
  compose with `And`, `Or`, `Not`.
- **Phase handles, not strings.** `DeclarePhase` returns a `PhaseHandle`; `go.To(handle)` cannot point at a missing
  phase. The name is only for logs, HUD and replication debugging.
- **What still fails at build time** (in `EventCatalogService`, so an EditMode test catches it): duplicate ids, a
  declared phase that is never defined, a phase no transition reaches, an action or condition without a handler in
  the profile.
- **Recipe location.** The events core (`Features/DesignedEvents/`) holds the builder, runtime and generic vocabulary
  (`Announce`, `Succeed`/`Fail`). Recipes, which mix features, live in `Features/DesignedEvents/Recipes/` and are
  registered by the events installer. Feature vocabulary (the `Ship()` scope and its handlers) lives with the feature.

### Transitions and branching (decision I)
- A phase has enter actions and an ordered list of exits: `When(condition, target)` exits are checked every tick in
  order and the first that holds wins; `After(duration, target)` fires when time in the phase runs out; `Then(target)`
  is an unconditional exit on the next tick. Targets are another phase, `Succeed()` or `Fail()`.
- **Linear shorthand** stays for simple events and expands to transitions: `.Until(condition).Within(seconds)` is
  `When(condition, next)` + `After(seconds, Fail)`; `.For(seconds)` is `After(seconds, next)`, where "next" is the
  next phase in declaration order.
- `EventRunProcessor` stays pure: (phase, which `When` exits hold, ticks in phase) → stay, go to phase, succeed or
  fail. Replication carries the phase id, so branching adds nothing there.

### Variation (decision J: in code)
Ordered by value; the first three are in scope:
1. **Ranges rolled at start.** `Range(1, 3)`, `Seconds(60, 90)` in the recipe; when an event starts, an `EventRoller`
   rolls every range with an injected random source (seeded in tests) into an `EventRun`, the per-run instance.
   Definitions stay immutable templates.
2. **Weighted and random exits.** `.Randomly(r => r.Weight(3, go => go.To(storm)).Weight(1, go => go.To(calm)))`,
   rolled when the phase is entered.
3. **Fragments.** Reusable, parameterised groups of phases (`e.Include(ShipFragments.Breakdown(parts: Range(1, 2)),
   exitTo: holds)`), so events are composed rather than copy-pasted.
4. Later: parameters scaled by context (crew size, fuel, time since the last event); modifiers applied to any event
   ("in fog").

### Execution: handlers from DI (decision F)
Unchanged from the POC: scope methods record data-only actions and conditions; the owning feature registers
`EventActionHandler<T>` / `EventConditionHandler<T>` in its installer; `EventHandlerRegistry` routes by type. Actions
run on the server only and are idempotent.

### Runtime
- `EventScheduleConfig` (on the events installer, per profile): which ids may start, with weight, cooldown and
  preconditions (conditions from the same vocabulary), plus a quiet gap. One event runs at a time (decision A).
- `EventDirectorUseCase` (server, `AfterAirship` tick) picks from the schedule, rolls the `EventRun`, and drives it
  with `EventRunProcessor`. Seconds are converted to ticks with `GameplayTicks.FromSeconds` when a phase starts.
- Breakage loses its own timer once the schedule exists (decision C). Flying cans move onto events only after
  `FlyingCanUseCase` gets an on-demand API (see the correction above).

### Replication (decision B)
A small world entity the events installer spawns (listed in its `NetworkedPrefabs`): `EventDirectorNetworkMediator`
next to an `EntityNetworkMediator`. State: event id, phase id, phase end in **server time** (ticks cannot be compared
across peers), last outcome, the current announcement, and the rolled values the HUD shows. The director writes it on
the server; `EventHudPresenter` reads it and the local catalog on every peer. Late joiners get it with the spawn (read
`.Value` in `OnNetworkSpawn`). Scenario probes then answer on clients too.

### Debugging
The director exposes a read-only snapshot: event, phase, time left, each exit's current value and the rolled values.
A DevTools HUD line shows it; scenario probes (`EventPhase`, `EventOutcome`) and the `StartEvent` command read and
drive it.

## POC (built 2026-09-28, developer: "build a POC ... keep it simple")
In code under `Features/DesignedEvents/` (row in `CODE_MAP.md`). What it proves: the builder, validation, the pure
phase machine on ticks, and handlers contributed by another feature through DI.
- Built: `EventDefinition.Builder`, `EventCatalog.HullStress` (announce, break parts 0 and 2, until repaired, 90 s
  timeout), `EventDirectorUseCase` (server; `TryStart`, auto-start rotation with first delay and quiet gap on the
  installer), `EventHandlerRegistry`, `Announce` (HUD line). Ship damage contributes `BreakShipPart` and
  `BrokenPartsAtMost`. The harness switches auto-start off for scripted runs (`HarnessGameplayOverrides`).
- Installer in `Profile_FuelSandbox` and `Profile_Test_ShipDamage`; scenario `HullStressEvent` passes solo and
  host + client (2026-09-28).
- Left out on purpose: replicated state (clients only see the effects; the HUD line is host-only), the schedule
  config (the POC rotates the whole catalog), flying cans (see the correction above), tracks.
- Kept by the design above: handlers, registry, director and processor roles, the harness switch, the scenario.
  Replaced: the static catalog, `new` for steps, linear-only phases, string phase names.

## Slices
- **E1a, authoring API:** `IEventRecipe`, `IEventBuilderFactory`, `EventCatalogService`, `EventId`/`EventIds`, staged
  builder with phase handles and transitions (plus linear shorthand), action and condition scopes, `Ship()` and
  `Fuel()` vocabulary; processor moved to transitions; HullStress rewritten as a recipe with a branch. Scenario
  `HullStressEvent` stays green.
- **E1b, replication:** the world entity and mediator, `EventHudPresenter`, client probes, scenario `HullStressEvent`
  checked on the client and a late-join scenario.
- **E1c, variation:** ranges and `EventRoller`/`EventRun`, weighted exits, fragments; seeded tests.
- **E1d, schedule and migration:** `EventScheduleConfig` (weights, cooldowns, preconditions), breakage off its own
  timer; flying cans once they have an on-demand API.
- **E2:** timed tracks inside a phase (decision E); targets: a target entity prefab (health, targetable),
  `SpawnTargets` action, `SpawnedGroupDestroyed` condition.
- **E3:** gunner: a cannon station on the ship and a fire ability (targeting + a damage effect). GAS cleanup lands here:
  implement `CancelAbilitiesWithTag` / `BlockAbilitiesWithTag` (needed for the station), delete `CostEffect` and
  `AbilityTag` unless ammo needs cost.

## Tests
- `EventBuilderTests`: staged API (the compile-time rules are covered by the types; tests cover the build-time ones),
  shorthand expansion, phase handles.
- `EventCatalogServiceTests`: every registered recipe builds, unique ids, unreachable or undefined phases rejected,
  missing handlers reported.
- `EventRunProcessorTests`: exit order, first match wins, `After` and `Then`, succeed/fail, condition before timeout.
- `EventRollerTests` (E1c): ranges and weighted exits with a seeded random source.
- `EventDirectorUseCaseTests`, `EventScheduleTests` (E1d), a test per handler, and the vocabulary scopes.
- Scenarios: `HullStressEvent` solo and host + client; a late-join scenario (E1b).

## Decisions
Taken with the developer (2026-09-28), all as recommended:
- **A. Concurrency:** one event at a time.
- **B. Where state lives:** a world entity spawned by the events installer.
- **C. Migration timing:** breakage moves onto events with the schedule (E1d); cans after they get an on-demand API.
- **D. Authoring:** code, structure and numbers in code (not a node graph, not ScriptableObjects).
- **F. Execution:** data-only actions and conditions, handlers registered by the owning feature through DI.
- **G. Construction:** recipes registered through DI and built by an injected builder factory; no static catalog.
- **H. Typing:** a staged fluent API with typed delegate scopes; feature vocabulary as extension methods.
- **I. Flow:** transitions with branching are the core model; linear phases are shorthand on top.
- **J. Variation:** ranges, weighted exits and fragments, all in code.
- **Order:** E1a authoring API, then E1b replication, then E1c variation, then E1d schedule.

Open:
- **E. Timed tracks:** in E2. Move earlier if an event needs a parallel lane before then.
