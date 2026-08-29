# TinCan Unity - Tools README

This folder contains automation scripts for project setup, maintenance, and CI/CD integration. Windows + PowerShell is the only officially supported and maintained path.

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

These scripts follow the standards in:
- [`.docs/AI_CONFIGURATION.md`](../.docs/AI_CONFIGURATION.md) - AI operational guidelines
- [`.docs/ARCHITECTURE.md`](../.docs/ARCHITECTURE.md) - System architecture
- [`.docs/CODE_STANDARDS.md`](../.docs/CODE_STANDARDS.md) - Coding standards

## Future Scripts

Additional scripts planned:
- `validate-packages.ps1/sh` - Verify package compatibility
- `sync-editor-prefs.ps1/sh` - Share editor preferences across team
- `build.ps1/sh` - Automated build for CI/CD (the Unity CLI's own `unity build`/`unity test` commands may cover this instead)

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
4. Document in this README
5. Reference relevant `.md` files in `.docs/`

---

**Last Updated:** January 18, 2026
