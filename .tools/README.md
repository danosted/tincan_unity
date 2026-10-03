# TinCan Unity - Tools README

This folder contains automation scripts for project setup, maintenance, verification and builds. Windows + PowerShell is the only officially supported and maintained path.

## Prerequisites

Windows App Installer, which provides `winget`, is the only manual prerequisite. The `setup.cmd` bootstrap installs PowerShell 7 and the [Unity CLI](https://docs.unity.com/en-us/unity-cli/unity-cli-reference) when needed, then installs the Editor version pinned in `.unity-version`.

## Scripts

### `setup.ps1`

**Purpose:** One-command project initialization
**When to use:** Running on a fresh clone or setting up a new developer machine
**Usage:**
```powershell
.\.tools\setup.cmd                         # fresh Windows machine
.\.tools\setup.cmd -EnableUnityTelemetry   # explicitly opt in to Unity CLI analytics
.\.tools\setup.ps1                         # direct use when PowerShell 7 is already installed
```

**What it does:**
- Installs PowerShell 7 via `winget` when launched through `setup.cmd`
- Validates folder structure
- Installs the Unity CLI via `winget` if it isn't already present
- Records Unity CLI telemetry consent without an interactive prompt (opt out by default)
- Checks Unity version from `.unity-version`
- Detects or installs the matching Editor via the Unity CLI, falling back to Unity Hub paths for detection
- Creates `.env` configuration file
- Windows Firewall rules for the game builds (`Builds/Win64`, `Builds/Win64Perf`): explains, asks `[y/N]`, then one administrator prompt; skipped with a warning when nobody can answer. Alone: `.\.tools\setup.ps1 -Only Firewall`
- Sets up Packages/manifest.json if needed
- Validates installation

### `upgrade-unity.ps1`

**Purpose:** Safely upgrade to a new Unity version
**When to use:** Anytime \u2014 this is a greenfield project, so the default is to stay on the latest Unity release
**Usage:**
```powershell
.\.tools\upgrade-unity.ps1                              # upgrade to the latest release
.\.tools\upgrade-unity.ps1 -TargetVersion "6000.4.2f1"  # pin to a specific version
```

**What it does:**
- Resolves and installs the target version via the Unity CLI (defaults to `latest`)
- Creates automatic backup
- Updates `.unity-version` (single source of truth for these scripts)
- Validates the upgrade
- Logs all changes

Note: `ProjectSettings/ProjectVersion.txt` is owned by the Unity Editor and is left untouched — it updates itself the next time the project is opened with the new Editor version.

### `verify.ps1`

**Purpose:** The feedback loop for a feature: compile, EditMode tests, then live scenarios (solo, host + client)
**Needs:** the Unity Editor open on the project, and Unity Hub running
**Usage:**
```powershell
.\.tools\verify.ps1 -UpTo Tests            # compile + EditMode tests
.\.tools\verify.ps1 -Scenario NetCatch     # + the scenario, solo and host + client (takes over the Editor)
.\.tools\verify.ps1 -All                   # every scenario
```
Exit code 0 pass, 1 a tier failed, 2 the Editor was not usable. Tiers, flags and the guards against Editor and MPPM
failures: [`.docs/NETWORK_TEST_HARNESS.md`](../.docs/NETWORK_TEST_HARNESS.md), "Scenarios".

### `build.ps1`

**Purpose:** Build the Linux dedicated server, its container image and (with `-Client`) the matching Windows client
**When to use:** Before running the server in a container (plan: `.docs/plans/dedicated-server-container.md`)
**Needs:** the Editor module `linux-server` (`unity editors module add <version> -m linux-server`, asks for admin rights; restart the Editor afterwards) and Podman with its machine running (`podman machine start`)
**Usage:**
```powershell
.\.tools\build.ps1             # Builds/LinuxServer/TinCanServer.x86_64 (in the open Editor, else batch mode)
.\.tools\build.ps1 -Image -Client  # then the image tincan-server:<commit> and :local, and Builds/Win64/TinCan.exe
.\.tools\build.ps1 -ImageOnly  # rebuild only the image
.\.tools\build.ps1 -Perf -Image -Client  # development "perf" variants: Builds/LinuxServerPerf, image tincan-server-perf, Builds/Win64Perf
podman run -d --name tincan-server -p 7777:7777/udp --restart unless-stopped tincan-server:local   # run it: UDP 7777
podman logs -f tincan-server   # the Unity log
podman rm -f tincan-server     # stop it
```
Players join with `TinCan.exe -autojoin <server IP>:7777`. Clients and server must come from the same commit.
`podman compose -f Container/server/compose.yaml up` also works, but `podman compose` needs a compose provider
installed (today it borrows Docker Desktop's `docker-compose`). The machine's CPU and memory come from WSL
(`%UserProfile%\.wslconfig`), not from `podman machine set`.

### `perf.ps1`

**Purpose:** Perf runs: a load scenario in containers (and optionally the desktop client), one folder per run under
`Logs/perf/runs/`, checked against `.docs/perf/budgets.json`
**Needs:** the perf builds (`.\.tools\build.ps1 -Perf -Image -Client`) and the Podman machine running
**Usage:**
```powershell
.\.tools\perf.ps1 run CrewLoad                 # server + 4 headless bots in containers (nothing opens on the desktop)
.\.tools\perf.ps1 run CrewLoad -Desktop        # 3 bots + the Windows perf client rendering (opens a window)
.\.tools\perf.ps1 list                         # run folders and verdicts
.\.tools\perf.ps1 compare <runA> <runB>        # budgeted metrics side by side
.\.tools\perf.ps1 trend CrewLoad server.tick_ms.p95
.\.tools\perf.ps1 set-budgets <run> <run> <run> -Rationale "why"
```
Exit code 0 pass (or no budgets yet), 1 a budget failed, 2 the environment was not usable. Budgets, profiles and how
to read a report: [`.docs/PERFORMANCE.md`](../.docs/PERFORMANCE.md).

## Modules

Entry scripts (`verify.ps1`, `build.ps1`, …) only parse their parameters, import what they need from `modules/` and run their
flow. Shared logic lives in the modules (plan: `.docs/plans/tools-modules.md`):

| Module | Holds |
|---|---|
| `TinCan.Common` | project root, `Start-ToolLog` + `Write-Log` (console and `logs/<name>-<time>.log`), `Write-Section`, `Write-Tier`, exit codes, `Stop-Unusable` |
| `TinCan.Editor` | `Invoke-Unity`, Editor readiness and recovery, modal dialogs, scenes, Play Unfocused |
| `TinCan.Mppm` | MPPM clone paths, clone scene sync, network prefab hash check |
| `TinCan.Verify` | the compile, test and scenario tiers, the batch table |
| `TinCan.Build` | player builds, in the open Editor (after checking it is not stalled) or a batch-mode one |
| `TinCan.Container` | container images and game containers through Podman (the engine is named once, `$Engine`): start with limits, wait, collect logs |
| `TinCan.Perf` | perf profiles and budgeted metrics, run folders, budget and drift checks, compare / trend / set-budgets |
| `TinCan.Host` | guided one-time Windows setup: firewall rules for the game builds (`Confirm-FirewallRules`) |

Rules for module code:
- **No `exit` in a module.** It would end the caller's script. Return a result or throw; `Stop-Unusable` throws an
  error the entry script turns into exit code 2 (`Get-UnusableReason` in its `catch`).
- **No reading the entry script's variables.** Pass them as parameters. Each module sets `Set-StrictMode -Version 1.0`,
  so an unset variable is an error, not a silent `$null`. (Not `Latest`: it also rejects reading a missing property,
  which the scripts rely on for CLI replies.)
- **Export explicitly** with `Export-ModuleMember`; a module imports the modules it uses itself.
- **Help stays on the entry script** (`Get-Help .\.tools\verify.ps1`).
- **One-time setup is a guided step.** A step that changes the machine (firewall rules, anything needing admin) checks
  what is missing, explains why, asks with `Confirm-Step` (`[y/N]`), and only then acts, with at most one admin
  prompt. Unattended (`Test-Interactive` is false: agents, redirected input, scheduled runs) it changes nothing and
  the caller stops with the command to run, so nothing ever hangs on a question or a Windows prompt.

Adding a module: create `modules/TinCan.<Name>.psm1` starting with `$ErrorActionPreference = "Stop"`,
`Set-StrictMode -Version 1.0` and an `Import-Module (Join-Path $PSScriptRoot "TinCan.<Dep>.psm1")` per dependency;
end with `Export-ModuleMember`. Entry scripts import it with `Import-Module (Join-Path $PSScriptRoot
"modules/TinCan.<Name>.psm1") -Force` (`-Force` picks up edits in a long-lived terminal). Add a row to the table above.

## Unity MCP

The workspace MCP configuration launches `unity mcp` through the Unity CLI. The required `com.unity.pipeline` package is pinned in `Packages/manifest.json`.

After initial setup:

1. Restart VS Code if setup installed the Unity CLI while VS Code was already open.
2. Open the project in Unity and wait for package import and script compilation to finish.
3. Trust and start the `unity` server when VS Code prompts.
4. Run `unity status` to diagnose Editor connectivity. If Pipeline is not installed, run `unity pipeline install --project-path .` while signed in to Unity.

Use MCP for live Editor inspection and serialized object changes. Continue using workspace tools for source files and use CLI Pipeline commands as the fallback when MCP is unavailable during a domain reload.

## Logs

All scripts write logs to `logs/` folder with timestamps:
```
logs/
├── setup-2026-01-18_14-23-45.log
├── upgrade-2026-01-18_15-12-30.log
└── ...
```

Review logs if something goes wrong during setup or upgrade.

## Integration with Documentation

Project documentation starts at [`.docs/README.md`](../.docs/README.md) (humans) and
[`AGENTS.md`](../AGENTS.md) (AI assistants). Daily Unity CLI commands are listed in the `.docs` hub.

## Future Scripts

Additional scripts planned:
- `validate-packages.ps1/sh` - Verify package compatibility
- `sync-editor-prefs.ps1/sh` - Share editor preferences across team
- CI/CD builds (`build.ps1` covers local player and image builds; the Unity CLI's own `unity build`/`unity test` commands may cover CI)

## Troubleshooting

### Script not executing (PowerShell)

Use the bootstrap launcher. It applies an execution-policy bypass only to the new setup process and does not change your user or machine policy:
```powershell
.\.tools\setup.cmd
```

### Permission errors on Windows

Run PowerShell as Administrator for file operations.

## macOS/Linux

Not officially supported or debugged. The `.sh` counterparts in this folder are best-effort and unmaintained — macOS/Linux users are on their own to adapt them.

## For Developers

When creating new scripts:
1. PowerShell (.ps1) only — this is the only path we test and support
2. Include proper error handling and logging
3. Write logs to `logs/` folder
4. Document in this README; put shared logic in a module (see [Modules](#modules))
5. Reference relevant `.md` files in `.docs/`
