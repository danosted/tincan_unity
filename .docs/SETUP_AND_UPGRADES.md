# Setup & Unity Version Upgrades

**Document Version:** 1.1
**Last Updated:** January 18, 2026
**Maintainer:** AI Assistant + Development Team

## Table of Contents

1. [Initial Project Setup](#initial-project-setup)
2. [Version Management Philosophy](#version-management-philosophy)
3. [Upgrading Unity Versions](#upgrading-unity-versions)
4. [Dependency Management](#dependency-management)
5. [Environment Setup](#environment-setup)
6. [Troubleshooting](#troubleshooting)

---

## Initial Project Setup

### Prerequisites

- **Unity Hub** installed (or use command-line Unity)
- **Git** installed and configured
- **PowerShell 5.1+** (Windows) or **Bash** (macOS/Linux)

### One-Command Setup

**Windows:**
```powershell
.\.tools\setup.ps1
```

**macOS/Linux:**
```bash
./.tools/setup.sh
```

This script will:
1. Read `.unity-version` to determine target version
2. Verify or install that Unity version
3. Download and configure required packages
4. Set up project metadata files
5. Validate the installation

### What Happens During Setup

The setup process:
- Creates `.env` file with project paths (gitignored)
- Validates all required folders exist
- Installs required packages from `manifest.json`
- Configures Unity to use this project
- Prepares documentation references

---

## Version Management Philosophy

### The Single Source of Truth: `.unity-version`

Located at project root, this file contains **only** the Unity version string:

```
6000.3.4f1
```

**Never manually edit version numbers elsewhere.** The upgrade script handles all references.

### Why This Approach?

✅ One place to look for the current version
✅ Upgrade scripts read this file consistently
✅ No scattered version numbers to desynchronize
✅ Easy for AI to analyze and reference
✅ CI/CD pipelines can parse it automatically

### Files Automatically Updated by Upgrade Script

When you change `.unity-version`, the upgrade script updates:

- `ProjectSettings/ProjectVersion.txt` - Unity's own file
- `Packages/manifest.json` - Package version compatibility
- `ProjectSettings/ProjectSettings.asset` - Editor settings
- This document (auto-adds a changelog entry)

---

## Upgrading Unity Versions

### Method 1: Interactive Upgrade (Recommended)

**Windows:**
```powershell
.\.tools\upgrade-unity.ps1 -TargetVersion "2023.3.0f1"
```

**macOS/Linux:**
```bash
./.tools/upgrade-unity.sh 2023.3.0f1
```

### What the Upgrade Script Does

1. **Validates the target version** exists in your Unity Hub
2. **Creates a backup** of current project (optional)
3. **Updates `.unity-version`** as single source of truth
4. **Migrates package compatibility** in manifest.json
5. **Updates ProjectSettings** files
6. **Runs API upgrade tool** (if available in target version)
7. **Validates no breaking changes** in your scripts
8. **Logs all changes** for your records

### Method 2: Manual Upgrade (If Script Fails)

If you need to upgrade manually:

1. **Update `.unity-version`:**
   ```
   2024.1.0f1
   ```

2. **Run validation:**
   ```powershell
   .\.tools\validate-version.ps1
   ```

3. **Open in Unity Hub:**
   - Unity Hub will detect version change
   - It will prompt to upgrade the project
   - Accept the upgrade

4. **Verify packages:**
   ```powershell
   .\.tools\validate-packages.ps1
   ```

---

## Dependency Management

### Git LFS Setup

For team projects with large assets, Git LFS prevents repository bloat:

**First-Time Setup:**
```bash
git lfs install
```

This configures your Git to use LFS (do once per machine).

**What's Tracked by LFS:**
See `.gitattributes` file (auto-configured):
- 3D Models (`.fbx`, `.obj`, `.blend`)
- Textures (`.png`, `.jpg`, `.psd`, `.exr`)
- Audio (`.wav`, `.mp3`, `.ogg`)
- Video (`.mp4`, `.mov`, `.webm`)
- Animations (`.anim`)

**Why LFS Matters:**
Without LFS, large assets bloat your `.git` folder. Unity projects easily reach 500MB+ with textures/models. LFS keeps history efficient.

**When You Clone:**
```bash
git clone <repository>
git lfs pull  # Download all LFS files
```

Git handles LFS transparently—just commit normally.

**Troubleshooting:**
```bash
git lfs ls-files    # See what's in LFS
git lfs pull        # Download LFS files if missing
```

### Understanding Our Package Structure

**Key Packages (required for this project):**

- **com.unity.netcode.gameobjects** - Multiplayer networking
- **com.unity.transport** - Low-level networking
- **com.unity.burst** - Performance optimization
- **com.unity.collections** - Advanced data structures

These are defined in `Packages/manifest.json`.

### Adding New Packages

**Via Package Manager (GUI):**
1. Window → TextAsset Manager → Package Manager
2. Add package by name or Git URL
3. Update script will auto-sync version to `.unity-version`

**Via Command Line:**
```powershell
.\.tools\add-package.ps1 -PackageName "com.my.package" -Version "1.2.3"
```

**Manual (experts only):**
Edit `Packages/manifest.json` and run:
```powershell
.\.tools\validate-packages.ps1
```

---

## Environment Setup

### IDE Configuration

**Visual Studio Code (.vscode/ folder contains):**
- `.vscode/settings.json` - C# and Unity settings
- `.vscode/launch.json` - Debugger configuration
- `.vscode/extensions.json` - Recommended extensions

**Recommended VS Code Extensions:**
```
- Unity Tools
- C# DevKit
- Debugger for Unity
- HLSL Tools
```

### Editor Preferences

The project comes with shared editor preferences in `ProjectSettings/EditorSettings.asset`:
- Code formatting standards
- Asset import defaults
- Debugging options

**To sync your local preferences with team:**
```powershell
.\.tools\sync-editor-prefs.ps1
```

---

## AI Integration Points

This document is AI-maintained. When you ask your AI assistant to help with:

- **Version upgrades** → Reference this doc, section "Upgrading Unity Versions"
- **New packages** → Reference "Dependency Management"
- **Setup issues** → Reference "Troubleshooting" or "Environment Setup"
- **Automation needs** → AI can create new scripts in `.tools/`

**Pattern for AI requests:**
> "Ref: SETUP_AND_UPGRADES.md - Environment Setup section. Please [task description]"

---

## Troubleshooting

### Common Issues

#### Issue: Setup script fails with "Unity not found"

**Solution:**
```powershell
# Specify Unity path explicitly
.\.tools\setup.ps1 -UnityPath "C:\Program Files\Unity\Hub\Editor\2023.2.15f1"
```

Or install via Unity Hub:
1. Open Unity Hub
2. Installations → Install Editor (select matching `.unity-version`)
3. Re-run setup script

#### Issue: Package conflicts after upgrade

**Solution:**
```powershell
# Validate and fix package compatibility
.\.tools\validate-packages.ps1 -AutoFix
```

This will:
- Remove incompatible packages
- Add required compatibility packages
- Update versions as needed

#### Issue: "Project is from a newer version of Unity"

**Solution:**
1. Check your `.unity-version` file
2. Ensure you have that version installed in Unity Hub
3. Close and reopen Unity if you just installed it
4. Delete `Library` folder (it will regenerate)

#### Issue: Can't find `.unity-version` file

**Solution:**
Ensure you ran the initial setup:
```powershell
.\.tools\setup.ps1
```

If `.unity-version` is missing, create it with your current version (find in About dialog in Unity Editor).

### Getting Detailed Logs

All scripts produce logs in `.tools/logs/`:

```powershell
Get-Content .\.tools\logs\upgrade-$(Get-Date -Format 'yyyy-MM-dd').log
```

---

## Version Upgrade Changelog

Track all version changes here (updated by upgrade scripts):

| Version | Date | Reason | Breaking Changes |
|---------|------|--------|-------------------|
| 6000.3.4f1 | Jan 18, 2026 | Upgrade to Unity 6.3 LTS | None (6.0+ series compatible) |
| 2023.2.15f1 | Jan 18, 2026 | Initial setup | N/A |

---

## Next Steps

1. **Run setup:** Execute `setup.ps1` or `setup.sh`
2. **Verify installation:** Check all folders are created
3. **Read CONTRIBUTING.md:** Understand the AI collaboration model
4. **Reference ARCHITECTURE.md:** Learn the code organization

For detailed information on each system, see the [`.docs/` folder contents](./README.md).

---

**Questions?** Each section above includes examples. For AI-assisted help, reference the section name in your request.
