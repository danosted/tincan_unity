Status: Done (2026-10-02)

# Tooling modules: split `.tools` scripts into modules with thin entry scripts

## Goal
`.tools/verify.ps1` (462 lines) holds four concerns: driving the Editor, keeping MPPM clones in sync, the
compile/test tiers and the scenario tier. The perf work ([`performance-budgets.md`](performance-budgets.md)) would add
builds, containers and budget checks on top. Split the shared logic into PowerShell modules; each entry script only parses
its parameters, imports the modules it needs and runs its flow.

Prerequisite for the performance-budgets plan (P0 build changes, P4 `perf.ps1`).

## Decisions (agreed with the developer)
1. **Several small `.psm1` files, imported directly** (`Import-Module "$PSScriptRoot/modules/TinCan.Editor.psm1"`).
   No manifests, no one-file-per-function layout. Switch to one `TinCan.Tools` module with a manifest only if the
   number of modules or their cross-dependencies make direct imports awkward.
2. **A pure move first.** The refactor changes no behavior. `verify.ps1` keeps its parameters, output, exit codes and
   timings. The `verify-feature` and `add-scenario` skills, the Copilot prompts and about 20 docs refer to it.
3. **`build-server.ps1` becomes `build.ps1`**, since it already builds the Windows client too. References are
   updated in the same change. No forwarding stub, since only docs and a skill call it.
4. **No PowerShell tests yet** (developer's call). The scripts are checked by running them (see [Verify](#verify)).
   Pester can come later if the modules grow logic worth testing on its own.

## Layout
```
.tools/
  verify.ps1  build.ps1  perf.ps1  setup.ps1  upgrade-unity.ps1   # entry scripts: params + flow
  modules/
    TinCan.Common.psm1
    TinCan.Editor.psm1
    TinCan.Mppm.psm1
    TinCan.Verify.psm1
    TinCan.Build.psm1
    TinCan.Container.psm1     # Podman
    TinCan.Perf.psm1          # added by the performance-budgets plan
```

| Module | Contents (from today's scripts) | Used by |
|---|---|---|
| Common | `Get-ProjectRoot`, `Write-Log`, `Write-Section` (now duplicated in `setup.ps1` and `upgrade-unity.ps1`), `Write-Tier`, exit-code constants, the "environment unusable" error | all |
| Editor | `Invoke-Unity`, `Test-EditorReady`, `Wait-EditorReady`, `Get-MainEditorProcessId`, `Invoke-FocusEditor`, `Get-EnvironmentProblem`, `Restore-Editor`, `Get-UnityModal`, `Confirm-ScenesClean`, `Open-ScenarioScene`, `Restore-StartScene`, `Set-PlayUnfocused` | verify, build, perf |
| Mppm | `Get-ClonePaths`, `Sync-CloneScenes`, `Confirm-NetworkPrefabsMatch` | verify |
| Verify | `Invoke-Compile`, `Invoke-Tests`, `Invoke-Scenario`, scenario-list lookup for `-All`, batch result table | verify, perf (`-Editor`) |
| Build | `Invoke-PlayerBuild` (open Editor or batch mode), `Read-Result`; later the perf variants and worktree builds | build, perf |
| Container | image build through Podman (today's `Invoke-ImageBuild`); later start/stop containers, wait for reports, collect logs | build, perf |

## Module rules
- **No `exit` inside a module.** `exit` in a module function ends the caller's whole script. Functions return a
  result or throw; only entry scripts exit. The "environment unusable" case (exit 2) becomes a typed error that the
  entry script maps to its exit code.
- **No script-scope state.** Today functions read `$ProjectRoot`, `$SaveDirtyScenes`, `$RestartEditor` and
  `$ScenarioTimeoutSeconds` from the script. In modules these become parameters (or a small context object the
  entry script builds once and passes in).
- **Export explicitly** with `Export-ModuleMember`; helpers stay private.
- **`$ErrorActionPreference = "Stop"` inside each module**, since a module does not inherit the caller's preference.
- **Comment-based help** stays on entry scripts (`Get-Help .\.tools\verify.ps1` keeps working). Module functions get
  a one-line synopsis.

## Pieces
### T1. Common + Editor + Mppm + Verify; slim `verify.ps1`
Move the functions as they are, apply the module rules, keep `verify.ps1`'s param block and main flow.

### T2. Build + Container; rename to `build.ps1`
Same treatment for `build-server.ps1`, and the image is built with Podman instead of Docker (developer's call).
`Docker/server/` becomes `Container/server/` with a `Containerfile` and `Containerfile.containerignore`. Update
`.tools/README.md`, `.docs/` and plan references (`dedicated-server-container.md` keeps its history but points to the
new names).

### T3. `setup.ps1`, `upgrade-unity.ps1` use Common
Drop the duplicated `Write-Log` / `Write-Section`. `Write-Log` takes the log file as a parameter (each script logs
to its own file under `.tools/logs/`).

### T4. Docs
`.tools/README.md` (layout, module rules, how to add a module), `.docs/CODE_MAP.md` folder map,
the `build-server.ps1` → `build.ps1` references.

## Verify
- `verify.ps1 -UpTo Tests` before and after the move: same output, same exit code (no confirmation needed).
- `build.ps1 -ImageOnly` (needs the Podman machine running).
- One confirmed play run at the end: `verify.ps1 -Scenario <one scenario>` (Solo + Duo, about 1 minute) to prove the
  scenario path is unchanged. Ask the developer first.

## Risks
- **Hidden script-scope coupling.** A function that silently read a script variable will get `$null` in a module.
  `Set-StrictMode -Version 1.0` in each module turns that into an error instead of a quiet wrong result. Not
  `Latest`: it also throws on reading a missing property, which the scripts do on purpose (a CLI reply that failed
  to parse is `{ raw }`, and `$status.compiling` is then `$null`).
- **Behavior drift in a pure move.** Keep T1 a move plus the module rules only; no clean-ups mixed in.

## Progress
- 2026-10-02: drafted. PowerShell tests (Pester) left out for now (developer's call).
- 2026-10-02: T1 built. `verify.ps1` 462 → 107 lines; `modules/TinCan.{Common,Editor,Mppm,Verify}.psm1`.
  Script-scope reads became parameters (`-RestartEditor`, `-SaveDirtyScenes`, `-TestFilter`, `-TimeoutSeconds`,
  `-KeepScene`); `Stop-Unusable` throws and `verify.ps1` maps it to exit 2. Run state (clone list, prefab check,
  return scene) is module state. `.tools/README.md` got a Modules section. Checked: `-UpTo Tests` output identical
  before and after (Compile PASS, 664/664, exit 0); an unknown scenario gives the same STOP line and exit 2. Open:
  the confirmed Solo + Duo run at the end of the plan.
- 2026-10-02: T2 built; Podman replaces Docker (developer's call). `build-server.ps1` → `build.ps1` (git mv), with
  `modules/TinCan.Build.psm1` and `modules/TinCan.Container.psm1`; functions return `$false` instead of exiting.
  `podman build` needs `--ignorefile` (only BuildKit finds `Dockerfile.dockerignore` by itself). References updated in
  `.tools/README.md`, `CODE_MAP.md`, `Network_Initialization_Flow.md`, the `PlayerBuild.cs` summary, and the
  Dockerfile and compose comments. Then (developer's call) `Docker/server/` → `Container/server/`, `Dockerfile` →
  `Containerfile`, `Dockerfile.dockerignore` → `Containerfile.containerignore` (git mv). Checked: `build.ps1 -ImageOnly` builds
  `tincan-server:<commit>` and `:local` in Podman (193 MB, Burst debug folder excluded); the server boots in Podman
  (`[Session] Starting a dedicated server on 0.0.0.0:7777`, ~12 % of one core, 131 MB); UDP published from the
  Podman machine reaches Windows on localhost. Not checked: a LAN join through Podman's port publishing; the player
  build path (unchanged code, needs an Editor build).
- 2026-10-02: T3 built. `setup.ps1` and `upgrade-unity.ps1` import Common; their copies of `Write-Log` /
  `Write-Section` and log-folder set-up are gone. `Start-ToolLog <name>` (Common) creates `.tools/logs/` and makes
  `Write-Log` append to `<name>-<timestamp>.log`. Checked by parsing both scripts and probing the log in isolation;
  neither script was run (setup rewrites `.env` and the CLI analytics consent; upgrade installs an Editor).
- 2026-10-02: T4 done. `.tools/README.md`: a `verify.ps1` section, the module table and "adding a module";
  `CODE_MAP.md`: a "Tooling (outside Assets)" folder table (`.tools/`, `.tools/modules/`, `Container/server/`,
  `Builds/`); `.docs/README.md` index row. Left: the confirmed full test run (developer will schedule it).
- 2026-10-02: full check. `build.ps1 -Image -Client`: Linux server (23 s), image, Windows client (12 s), exit 0.
  `verify.ps1 -Scenario NetCatch`: Solo PASS (9 s), Duo PASS (14 s). The first attempt stopped with exit 2 (no
  Player 2 running), which exercised the module STOP path; Player 2 was then launched through
  `NetHarnessPlayerTagsMenu.EnsureLaunched`. Known (unchanged from before the split): the STOP path does not reopen
  the start scene.
