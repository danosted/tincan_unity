# TinCan docs: start here (humans)

This folder is the map of the project for people. AI agents read [`AGENTS.md`](../AGENTS.md), which points
back here; the two must never disagree, so when you change how the project works, change the doc, not the agent
instructions.

## Start here

Pick the path that matches what you want to do right now.

| I want to... | Do this |
|---|---|
| **Run it** | `.\.tools\setup.cmd` once, open the folder in Unity, open `Assets/Scenes/drm_cloud_environment.unity`, press Play, click **Start Host** in the menu. "No cameras rendering" before Start Host is expected: the camera lives on the player prefab. |
| **Change something** | Find the matching recipe in [`TASK_GUIDES.md`](TASK_GUIDES.md). Each one lists exactly which files to open. |
| **Build a new feature** | Follow [`TUTORIAL_NEW_FEATURE.md`](TUTORIAL_NEW_FEATURE.md). It copies the Fuel feature step by step. |
| **Find where something lives** | [`CODE_MAP.md`](CODE_MAP.md): assemblies, folders, naming, "where does X live", feature index. |
| **Understand what the AI just did** | Recipe 9 in [`TASK_GUIDES.md`](TASK_GUIDES.md#9-review-what-the-ai-just-did), then playtest. |

## The 60-second mental model

- **Three assemblies, one direction.** `TinCan.Core.Domain` (contracts) <- `TinCan.Features` (gameplay) <-
  `Assembly-CSharp` (composition root, NGO glue, UI views). Nothing points the other way. Unit tests can see only
  the first two, so logic you want tested goes in `Features` or `Core.Domain`.
- **A feature is one folder plus one asset.** Code in `Assets/Scripts/Features/<X>/`, and a `FeatureInstaller`
  asset in `Assets/Resources/Installers/`. The installer registers services, lists networked prefabs and ship
  fixtures. No shared file is edited. Older features still register directly in `ProjectLifetimeScope.cs`; that
  is legacy.
- **Three kinds of class.** `*Processor` is pure math (tested first). `*UseCase` orchestrates and owns a
  VContainer lifecycle (`ITickable`, `ISimulationTickable`). `*NetworkMediator` is the thin NGO adapter that
  implements a domain interface and guards writes with `IsServer`. `*View` is a MonoBehaviour that only renders.
- **The server owns state, on a fixed tick.** `NetworkSimulationScheduler` runs the network tick: airship ->
  `AfterAirship` features -> cloud boundary -> physics sync -> humanoid -> `AfterHumanoid` features. Time-critical
  intent travels as bits in an `InputState`, never as a side-channel RPC.
- **Behaviour is data.** Abilities, effects, tags, interactions, menus, fixtures and tunables are ScriptableObject
  assets created from the **TinCan** create menu. Many changes need no code at all.
- **Tests are EditMode, NUnit, hand-written fakes.** `Assets/Tests/EditMode/`, fakes in `Fakes/`. No PlayMode
  suite, no mocking library.

## Map of the docs

| Doc | Read it when |
|---|---|
| [`CODE_MAP.md`](CODE_MAP.md) | You need to find a file, learn a suffix, or see which features exist and in which style. |
| [`TASK_GUIDES.md`](TASK_GUIDES.md) | You have a concrete task: tune a number, add an interaction, add a HUD value, write a test, playtest with two players, review AI work. |
| [`TUTORIAL_NEW_FEATURE.md`](TUTORIAL_NEW_FEATURE.md) | You are building a whole feature and want the build order with a real file to copy at each step. |
| [`ARCHITECTURE.md`](ARCHITECTURE.md) | You want the why: DI, input-driven simulation, possession, registries, and what happens from Play to the tick loop. |
| [`FEATURE_INSTALLERS.md`](FEATURE_INSTALLERS.md) | Reference for `FeatureInstaller`, ship fixtures, networked prefabs, and the UnityYAMLMerge setup. |
| [`UI_FRAMEWORK.md`](UI_FRAMEWORK.md) | Reference for menus (`MenuDefinition`), `IMenuSystem`, `IMenuCommand`, `IHudValues`, Cancel-key ownership. |
| [`Network_Initialization_Flow.md`](Network_Initialization_Flow.md) | Deep dive: how VContainer and NGO initialise across host, server and client. |
| [`CODE_STANDARDS.md`](CODE_STANDARDS.md) | C# rules everyone follows (naming, guard clauses, nullable, no regions). |
| [`AI_CONFIGURATION.md`](AI_CONFIGURATION.md) | How AI assistants are expected to behave here. Read it so you know what to expect from them. |
| [`../.tools/README.md`](../.tools/README.md) | Setup and upgrade scripts, Unity CLI and MCP configuration. |

## Daily commands

All from the repo root with the Editor open. `unity status` must report `ready` first.

```bash
unity status
```
```bash
unity cmd recompile && unity cmd recompile_status
```
```bash
unity cmd run_tests --mode EditMode
```
```bash
unity cmd console --level error
```
```bash
unity cmd editor_play
```
```bash
unity cmd editor_stop
```

There is no CI. Running the EditMode tests and checking the console for errors before a PR is on you.

## Team conventions

- Branches are `<initials>/<topic>` (`cfb/`, `cheesed/`, `drm/`). PRs go to `danosted/tincan_unity` `main`,
  rebased on `origin/main` first.
- Prefabs, scenes and assets merge with Unity's own tool. Register the driver once; see
  [FEATURE_INSTALLERS.md, "Merging Unity YAML"](FEATURE_INSTALLERS.md#merging-unity-yaml).
- Binary assets are in Git LFS: run `git lfs install` once per machine.
- New `NetworkBehaviour` classes end in `NetworkMediator`. New files start with `#nullable enable`.
- When a feature lands, its row goes into the feature index in [`CODE_MAP.md`](CODE_MAP.md#feature-index).

## Proposed improvements (not done yet)

Ranked by value for effort. Tick them off as they land.

- [ ] **Change notes per PR.** AI writes `.docs/changes/YYYY-MM-DD-<topic>.md`: what changed, why, which files
      to read first, what to look at in the Editor, how to playtest, which tests cover it, open questions. The
      shape already exists in `.github/agents/session-handoff.agent.md`. Mandate it in `AGENTS.md`.
- [ ] **AI attribution in git.** A commit trailer (`AI-Assisted: <tool>` or `Co-Authored-By`) so `git log` shows
      which commits to review more carefully. Add a `.mailmap` merging Dan's two identities.
- [ ] **PR template** (`.github/PULL_REQUEST_TEMPLATE.md`) with a human review checklist: change note linked,
      tests green, feature index updated, assets touched listed, playtested host + client.
- [ ] **Stop gitignoring `.docs/plans/`.** Plans are the best record of AI intent and currently never reach
      teammates. Add a `Status: Draft | Approved | Done` header instead.
- [ ] **Repo hygiene PR.** Delete the empty scaffold folders, move `ThirdPersonCharacter`, rename the duplicate
      `IShipState`, remove the two deleted scenes from Build Settings. See CODE_MAP "Legacy, oddities and traps".
- [ ] **Editor window `TinCan > Feature Overview`** listing installers, fixtures, networked prefabs, tickables
      and handlers, and validating that every `IA_*` handler type and `Menu_*` command id still resolves. Catches
      the silent-break bugs.
- [ ] **`.tools/test.ps1` and `.tools/check-docs.ps1`**: run EditMode tests; verify every backticked path in
      `.docs/*.md` exists. Stepping stone to CI.
- [ ] **Migrate legacy features onto installers** (airship, humanoid, possession, build mode), one PR each, so
      `ProjectLifetimeScope.cs` stops being a merge hotspot.
- [ ] **GitHub Actions EditMode tests** via the Unity CLI. Blocked on licence and runner; standalone builds are
      also blocked today (see CODE_MAP traps).
