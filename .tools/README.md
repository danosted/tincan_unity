# TinCan Unity - Tools README

This folder contains automation scripts for project setup, maintenance, and CI/CD integration. Windows + PowerShell is the only officially supported and maintained path.

## Prerequisites

Install the [Unity CLI](https://docs.unity.com/en-us/unity-cli/unity-cli-reference) — this is required, not optional:
```powershell
winget install Unity.CLI
```
`setup.ps1` and `upgrade-unity.ps1` depend on it (`unity editors -i`, `unity install <version>`) to detect and install Unity Editor versions.

## Scripts

### `setup.ps1`

**Purpose:** One-command project initialization
**When to use:** Running on a fresh clone or setting up a new developer machine
**Usage:**
```powershell
.\.tools\setup.ps1
```

**What it does:**
- Validates folder structure
- Installs the Unity CLI via `winget` if it isn't already present
- Checks Unity version from `.unity-version`
- Detects an installed matching Editor via the Unity CLI, falling back to Unity Hub paths
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

You may need to allow script execution:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
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
