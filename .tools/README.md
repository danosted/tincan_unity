# TinCan Unity - Tools README

This folder contains automation scripts for project setup, maintenance, and CI/CD integration.

## Scripts

### `setup.ps1` / `setup.sh`

**Purpose:** One-command project initialization
**When to use:** Running on a fresh clone or setting up a new developer machine
**Usage (Windows):**
```powershell
.\.tools\setup.ps1
```
**Usage (macOS/Linux):**
```bash
./.tools/setup.sh
```

**What it does:**
- Validates folder structure
- Checks Unity version from `.unity-version`
- Creates `.env` configuration file
- Sets up Packages/manifest.json if needed
- Validates installation

### `upgrade-unity.ps1` / `upgrade-unity.sh`

**Purpose:** Safely upgrade to a new Unity version
**When to use:** When you need to update the project to a different Unity release
**Usage (Windows):**
```powershell
.\.tools\upgrade-unity.ps1 -TargetVersion "6000.4.2f1"
```
**Usage (macOS/Linux):**
```bash
./.tools/upgrade-unity.sh 6000.4.2f1
```

**What it does:**
- Creates automatic backup
- Updates `.unity-version` (single source of truth)
- Updates ProjectSettings files
- Validates the upgrade
- Logs all changes

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

These scripts reference and follow the standards in:
- [`SETUP_AND_UPGRADES.md`](../.docs/SETUP_AND_UPGRADES.md) - Detailed setup and upgrade guide
- `PROJECT_MANIFEST.md` - Project structure and `.unity-version` philosophy

## Future Scripts

Additional scripts planned:
- `validate-packages.ps1/sh` - Verify package compatibility
- `sync-editor-prefs.ps1/sh` - Share editor preferences across team
- `build.ps1/sh` - Automated build for CI/CD

## Troubleshooting

### Script not executing (PowerShell)

You may need to allow script execution:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

### Script not executing (Bash)

Make scripts executable:
```bash
chmod +x ./.tools/setup.sh
chmod +x ./.tools/upgrade-unity.sh
```

### Permission errors on Windows

Run PowerShell as Administrator for file operations.

## For Developers

When creating new scripts:
1. Add both PowerShell (.ps1) and Bash (.sh) versions if they're for setup/maintenance
2. Include proper error handling and logging
3. Write logs to `logs/` folder
4. Document in this README
5. Reference relevant `.md` files in `.docs/`

---

**Last Updated:** January 18, 2026
