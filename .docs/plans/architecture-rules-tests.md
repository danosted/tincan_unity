Status: Done (2026-09-27)

# Architecture rules as tests (slice 1 of `architecture-review.md`)

## Context
The project's rules live in prose (`AGENTS.md`, `CODE_STANDARDS.md`, `ARCHITECTURE.md`), and nothing checks them, so
drift is found by review or not at all (review finding D16). This slice turns the checkable rules into one EditMode
suite that fails on new violations and records today's offenders in lists that may only shrink. No production code
changes.

## Design
One test class, `Assets/Tests/EditMode/ArchitectureRulesTests.cs`, and its known offenders in
`ArchitectureRulesBaseline.cs` next to it: plain lists and numbers, reviewed like code.

Two mechanisms:
- **Allowlist** (named offenders). A test fails on a violation that is not listed. It also fails on a listed entry that
  no longer violates ("fixed: remove it from the baseline"), so the list can only shrink.
- **Ratchet** (a counted limit). A test fails when the count goes above the limit, and also when it drops below
  ("improved: lower the limit to N"), so every gain is locked in.

| Rule | Source | How it is checked | Baseline today |
|---|---|---|---|
| Every `NetworkBehaviour` ends in `NetworkMediator` | AGENTS.md, CODE_STANDARDS §5 | reflection over the loaded `TinCan.*` and `Assembly-CSharp` assemblies | allowlist: `AirshipControlPanel`, `AirshipDoor`, `ToggleShipTagStation`, `CoordinatedEventMediator` |
| No global or scene lookups in `Features/`: `NetworkManager.Singleton`, `Camera.main`, `Find*ObjectsBy*`/`FindAnyObjectByType`/`FindObjectOfType`, `GameObject.Find`, `Resources.FindObjectsOfTypeAll` | ARCHITECTURE §1, §5 | source scan of `Assets/Scripts/Features/**/*.cs` | allowlist per (file, pattern): `ThirdPersonLookView`, `InteractorControllerView`, `CloudEnvironmentView`, `GasChallengeUseCase` |
| No new registrations in `ProjectLifetimeScope` | AGENTS.md, ARCHITECTURE §7 | count `builder.Register`/`RegisterInstance`/`RegisterFactory`/`entryPoints.Add` in the file | ratchet: today's count |
| Every processor, use case and interaction handler has a test | AGENTS.md, CODE_STANDARDS §7 | reflection finds concrete `*Processor`/`*UseCase`/`*InteractionHandler` types; the type name must appear in a file under `Assets/Tests/` | allowlist: the untested types listed in review finding C13 |
| `#nullable enable` in every script | CODE_STANDARDS §4 | source scan of `Assets/Scripts/**/*.cs` | ratchet: files without it (120 today) |
| No `#region`, no coroutines | CODE_STANDARDS §1, §6 | source scan | none (zero today) |

Details:
- Source scans read files through `Application.dataPath`, skipping `Assets/ThirdParty`, and match whole tokens with
  simple regexes, not a C# parser. Comments can produce false positives; an allowlist entry can explain one.
- Failure messages name the rule, the file or type, the doc that states the rule, and the fix ("rename to …",
  "inject … instead", "write an installer").
- One type per file is left out: too fuzzy to scan reliably.

## Docs
- `CODE_STANDARDS.md` §7: the suite exists, and a baseline entry is removed when fixed, never added without the
  developer's agreement.
- `TASK_GUIDES.md` "Review what the AI just did": run the suite and read its failures.
- `architecture-review.md`: mark D16 done.

## Verification
- EditMode: the suite passes on today's code with the baselines above.
- It fails as intended on a throwaway local change: a misnamed `NetworkBehaviour`, `Camera.main` in a feature, an
  extra registration in the scope. Reverted before commit.
- No scenario run needed (tests only), but the full EditMode suite stays green.

## Outcome (2026-09-27)
- 6 tests, green on today's code; full EditMode suite 433 tests, green.
- Throwaway violations (a misnamed `NetworkBehaviour` with `Camera.main` and no `#nullable`, one extra scope
  registration) failed four rules with the intended messages; reverted.
- Baseline differs from the table above: naming adds `HumanoidPlayer` and `NetworkTransformMediator`; untested adds
  three interaction handlers, `InteractivityUseCase`, and two DevTools use cases; scope limit is 52.
- The coroutine scan matches `StartCoroutine`/`StopCoroutine` and `new WaitFor…`/`new WaitUntil`/`new WaitWhile`,
  because the scenario DSL has a `.WaitUntil(...)` step.
- Test files named `ArchitectureRules*` do not count as tests of a type (the baseline names the untested types).
