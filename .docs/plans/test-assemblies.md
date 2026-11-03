Status: Done (2026-11-03): T0–T6 built, 1371/1371 green; open: `-Changed` (T5) and the compile-time comparison

# Test assemblies: one per assembly under test

## Why
All 206 EditMode test files sit flat in one assembly, `TinCan.Tests.EditMode`, which references almost every
assembly in the project. That has three costs, and they grow with each feature:
- **No overview.** Nothing groups the tests, so finding the tests for a feature means searching by name.
- **Every change recompiles every test.** The test assembly depends on everything, so a one-line edit in any feature
  recompiles all 206 files.
- **No boundary in tests.** The one-way rule (core never sees a feature, a feature sees only what it builds on)
  holds for the code but not for its tests: any test can reach any assembly.

The developer chose (2026-11-03) the long-term structure over folders in one assembly: each assembly gets its own
test assembly, so tests follow the same boundaries as the code, and agents copy that shape for every new feature.

## The rule
- An assembly `X` with tests has exactly one test assembly, `X.Tests`, Editor only, with the define constraint
  `UNITY_INCLUDE_TESTS`. Examples: `TinCan.Features.Shipyard.Tests`, `TinCan.Ship.Tests`.
- `X.Tests` references `X`, what `X` references (only the parts it uses), the test framework, the shared fakes, and
  the test assemblies of what `X` builds on when it needs their fakes. Never a feature `X` does not build on.
- A test lives in the test assembly of the class it tests. A test that crosses several features (composition,
  loadouts, built-in ships) lives with the highest of them, or in `TinCan.Tests.Architecture` when it checks the
  whole project.
- Fakes live with the interface they fake: a fake of a core interface in `TinCan.Tests.Fakes`, a fake of a feature's
  interface in that feature's test assembly, under `Fakes/`.
- Rule tests check all of this, so the structure cannot drift.

## Where the tests go today
Mapped by the class each test file is named after (2026-11-03): 171 of 206 files land in 38 test assemblies. The
biggest: ShipDesigns 17, Gas 14, UI 10, Humanoid 8, then Input, Shipyard, Fuel, Damage and Core.Domain at 7 each.
The other 35 are placed by hand: cross-cutting tests (`ArchitectureRulesTests`, `FeatureCompositionTests`,
`InstallerServiceCheckTests`, `MainMenuCompositionTests`) go to `TinCan.Tests.Architecture`; the rest test a type
whose name differs from the file (`GasFieldTests`, `SkyHazardTests`, `MenuCommandTests`, ...).

New shared assemblies:
| Assembly | Holds |
|---|---|
| `TinCan.Tests.Fakes` | Fakes of core interfaces (most of today's `Fakes/`), Editor only |
| `TinCan.Tests.Architecture` | Rule tests and whole-project checks; the one test assembly that may reference everything |
| `TinCan.Tests.Shared` | Unchanged: fakes usable outside Editor-only assemblies |

## Decisions (developer, 2026-11-03)
1. **Next to the code.** `X.Tests` lives in a `Tests/` folder beside `X`'s asmdef, for example
   `Features/Shipyard/Tests/TinCan.Features.Shipyard.Tests.asmdef`. A feature folder holds everything it is, and
   deleting a feature deletes its tests. The nested asmdef keeps the tests out of `X` itself.
2. **Strict, one per assembly.** `Airship.Fuel` and `Airship.Fuel.Minigame` each get their own test assembly, and
   so do `CloudBoundary` and `CloudBoundary.Atmosphere`. No exceptions in the rule.
3. The shared test assemblies (`TinCan.Tests.Fakes`, `TinCan.Tests.Architecture`, `TinCan.Tests.Shared`) stay under
   `Assets/Tests/`, which then holds only them.

## Slices
Each slice ends with `verify.ps1 -UpTo Tests` green and the test count unchanged (206 files, same number of tests).

- **T0 Wait.** Starts after hosting-and-joining S1 lands: that session is editing test files and the test asmdef.
  Record the test count and the `-UpTo Tests` time as the baseline.
- **T1 Shared assemblies.** Create `TinCan.Tests.Fakes` and `TinCan.Tests.Architecture`; move the core fakes and the
  cross-cutting tests. The old assembly references the new ones until it is empty.
- **T2 Core.** One test assembly per core assembly (Domain, Entities, Gas, Humanoid, Input, Interaction, Items,
  Possession, RandomStreams, Settings, Ship, Targeting, UI, Infrastructure), then Network, App, DevTools.
- **T3 Features.** One test assembly per feature assembly; feature fakes move with their feature. Golden files
  (`Tests/EditMode/ShipDesigns/`) move to ShipDesigns' test folder; update `ShipDesignJsonCodecTests.GoldenPath` and
  the doc comment in `ShipDesignFormat.cs`.
- **T4 Rules.** Delete `TinCan.Tests.EditMode`. Rule tests: every `*Tests.cs` sits in `X.Tests` for the `X` its
  subject lives in; a test assembly references no feature its subject does not build on; every test assembly is
  Editor only with `UNITY_INCLUDE_TESTS`; `ProcessorsUseCasesAndHandlers_HaveTests` reads every test folder.
- **T5 Partial runs.** `verify.ps1 -TestAssembly <name>[,<name>]` runs only those test assemblies (check the
  installed `unity cmd test run` for an assembly filter; fall back to a name filter on the namespace). Later, for
  speed: `-Changed`, the test assemblies of the assemblies touched since `HEAD`.
- **T6 Docs.** CODE_MAP (assemblies table, folder map, `Fake*` glossary row, traps), CODE_STANDARDS (tests section),
  TASK_GUIDES and TUTORIAL_NEW_FEATURE (a new feature creates its test assembly next to its asmdef; the "add your
  assembly to `TinCan.Tests.EditMode.asmdef`" step and trap go), AGENTS.md (modification protocol), README.

## How the move is done
- Files move with their `.meta` (`git mv` both), so asset GUIDs stay and nothing in scenes or prefabs breaks.
- Asmdefs in this project reference by name, so each new asmdef is plain JSON; a one-off script drafts them from the
  mapping, then each is trimmed to what its tests use.
- Namespaces follow the test assembly (`TinCan.Features.Shipyard.Tests`). The `Fakes` namespace becomes
  `TinCan.Tests.Fakes`, which also fixes the `FakeMovingGround` namespace trap in CODE_MAP.

## Risks
- Tests now sit under `Assets/Scripts/`, so the rule tests that scan source (naming, global lookups, "processors
  have tests") must skip every `Tests/` folder as code, and read them as tests. Done in T1, before the first move.
- About 40 new assemblies. Unity compiles many small assemblies well, and the gain is that an edit recompiles only
  what depends on it; measure the T0 baseline against the T4 result.
- Known case (from hosting-and-joining S1): `FakeSessions` uses `TinCan.Network`'s `DirectSessionService`, so it goes
  in `TinCan.Network.Tests` under `Fakes/`, not in `TinCan.Tests.Fakes` (core fakes never reference Network).
  `FakeNetworkConnections` and `FakePlayerSettings` are core fakes.
- Fakes used across features may show a feature's tests reaching into another feature: each case is either a real
  dependency (reference the other feature's test assembly) or a fake that belongs in core.

## Progress (2026-11-03)
- **T0.** Baseline: 1369 tests green, `-UpTo Tests` 52 s (compile 17 s).
- **T1–T3.** 246 files moved with their `.meta` into 39 `X.Tests` assemblies beside their code, plus
  `TinCan.Tests.Fakes` (27 core fakes), `TinCan.Tests.Architecture` (5) and `TinCan.Tests.Integration` (2:
  `GasPocketChartSourceTests`, `EntityTests`). `TinCan.Tests.EditMode` is gone. Golden files moved to
  `Features/ShipDesigns/Tests/GoldenFiles/`.
- **Changes the one-way rule forced** (tests kept their assertions):
  `FakeInputContexts.Helmsman()` became `FakeHelmInputContexts` in Helm's tests; `FakeEquipment.cs` split into the core
  `FakeEquipmentActor`, Fuel's `FakeJerryCanSupply` and Minigame's `FakeNetRack`; `TargetableRegistrationTests` uses a
  core `FakeTargetables` instead of ship damage points; `ShipyardUseCaseTests` uses a new core `FakeSessionService`.
  `CommandLineSessionBootstrapTests` and `MainMenuBootstrapTests` drive the real `DirectSessionService`, so they live
  in `TinCan.Network.Tests`, the highest assembly they use.
- **T4.** Rule tests `TestAssemblies_SitBesideWhatTheyTest` and `TestAssemblies_FollowTheOneWayRule`; the source scans
  skip `Tests/` folders as code and read them as tests. `FeatureInstallerCatalog.IsCore` and `InputMapWriter` skip
  `*.Tests` assemblies too.
- **T5.** `verify.ps1 -TestAssembly <a>[,<b>]` (one runner call per assembly; the runner's assembly filter matches by
  part of the name). Open: `-Changed`.
- **T6.** CODE_STANDARDS §7, CODE_MAP, TASK_GUIDES §7, TUTORIAL_NEW_FEATURE (step 1 creates the test assembly),
  FEATURE_INSTALLERS, README, AGENTS.md, the verify-feature skill, NETWORK_TEST_HARNESS, `.tools/README.md`.
- **Left as is.** Each `X.Tests` references the whole of what `X` builds on (allowed by the rule) rather than only
  what its tests touch; trim when one gets in the way. The compile-time gain is not measured yet: compare an edit to
  one leaf feature against the T0 baseline.
