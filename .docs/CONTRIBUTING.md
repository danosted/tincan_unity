# Contributing Guide

**Document Version:** 1.0
**Last Updated:** January 18, 2026
**Scope:** All contributors (human and AI)

## Overview

This project uses **AI-assisted development** as a core workflow. This document explains how to work effectively with the AI assistant and your human teammates.

## For Human Contributors

### Before You Start

1. **Review the relevant `.md` files in `.docs/`** for the system you're working on
2. **Check the PROJECT_MANIFEST.md** to understand folder structure
3. **Verify your Unity version** matches `.unity-version`

### Working with the AI Assistant

The AI assistant is your development partner. Use these patterns:

#### Pattern 1: Implement a Feature

**What to provide:**
```
Task: [Feature name]
System: [Which system - Networking/FPS/UI/etc]
Requirements:
- [Requirement 1]
- [Requirement 2]
Reference Docs: [Any .docs/*.md files relevant]
Architecture Note: [If there's a specific design constraint]
```

**Example:**
```
Task: Implement player spawn synchronization
System: Networking
Requirements:
- Spawn all players at team-specific spawn points
- Handle late-joiners correctly
- Validate spawn positions before allowing
Reference Docs: NETWORKING.md, ARCHITECTURE.md
Architecture Note: See player spawning section in ARCHITECTURE.md
```

#### Pattern 2: Debug or Fix Issue

**What to provide:**
```
Issue: [What's broken]
System: [Which system]
Error: [Error message, if any]
Steps to reproduce: [How you triggered it]
Expected behavior: [What should happen]
Reference Docs: [Relevant docs]
```

**Example:**
```
Issue: Network messages not reaching other players
System: Networking
Error: NetworkMessage handler not triggered on receiving client
Steps to reproduce: Run two clients, send RPC, second client doesn't receive
Expected behavior: All RPC calls should arrive at all clients
Reference Docs: NETWORKING.md - Message Handling section
```

#### Pattern 3: Ask for Code Review

```
Review Request: [Component/Script name]
Focus: [What aspects to review - performance/correctness/style/all]
File: [Path to file]
Reference Docs: [Architecture or style guide]
```

### Code Standards

Follow these standards (enforced by AI assistance):

**File Organization:**
```
Assets/Scripts/[System]/
  ├── [Feature]Controller.cs
  ├── [Feature]Manager.cs
  ├── [Feature]UI.cs
  └── Editor/               # Editor-only code
```

**Naming Conventions:**
- Classes: `PascalCase` (e.g., `PlayerMovement`)
- Methods: `PascalCase` (e.g., `SpawnPlayer()`)
- Private fields: `_camelCase` (e.g., `_playerHealth`)
- Constants: `UPPER_SNAKE_CASE` (e.g., `MAX_PLAYERS`)

**Documentation Requirements:**
Every public class and method must have a summary:
```csharp
/// <summary>
/// Handles player input and movement for first-person controller.
/// Works with both single-player and networked multiplayer.
/// </summary>
public class PlayerMovement : MonoBehaviour
{
    /// <summary>
    /// Applies movement force based on input. Networked for multiplayer.
    /// </summary>
    public void ApplyMovement(Vector3 direction) { }
}
```

### Commit Message Standards

Use this format:
```
[System] Brief description

Detailed explanation if needed.

Closes #123
Reference: ARCHITECTURE.md - PlayerController section
```

**Example:**
```
[Networking] Add spawn point synchronization

- Implements INetworkSerializable for spawn data
- Validates positions before allowing spawn
- Handles late-joiner scenarios

Closes #15
Reference: ARCHITECTURE.md - Network Synchronization section
```

---

## For AI Assistants

### Important: Read AI_CONFIGURATION.md First

Before taking any action on this project, read [AI_CONFIGURATION.md](./AI_CONFIGURATION.md).

It defines:
- ✅ How to check current documentation (not cached knowledge)
- ✅ Pre-implementation checklist
- ✅ When to ask for user input
- ✅ How to handle April 2024 knowledge cutoff
- ✅ Version verification requirements

**This is your operational guide for this project.**

### Your Role in This Project

You are:
- ✅ The primary implementer of features
- ✅ The maintainer of `.docs/` documentation
- ✅ The analyzer of architecture and design patterns
- ✅ The reviewer of code quality
- ✅ The creator/updater of automation scripts in `.tools/`

You are NOT:
- ❌ The final decision maker on game design
- ❌ Able to commit code without human approval
- ❌ Responsible for testing on actual hardware
- ❌ The authority on gameplay feel and balance

### How to Maintain Documentation

When implementing a feature:

1. **Create or update the relevant `.docs/[Feature].md` file:**
   ```markdown
   # [Feature Name] System

   ## Overview
   [What this system does]

   ## Architecture
   [Class diagrams, data flow]

   ## Key Classes
   [List of main classes and their responsibilities]

   ## Usage Example
   [Code example showing how to use]

   ## Configuration
   [Any tweakable parameters]

   ## Known Limitations
   [Current constraints or TODO items]
   ```

2. **Update PROJECT_MANIFEST.md** with the new doc reference

3. **Link from parent docs** (e.g., if implementing Player spawning, link from NETWORKING.md)

### Code You Should Write

✅ Write code in `Assets/Scripts/` following standards above
✅ Create documentation in `.docs/`
✅ Write utility scripts in `.tools/`
✅ Create `.github/` workflows for CI/CD

**Always include comments explaining "why" not just "what":**
```csharp
// Validate spawn position before allowing player to spawn
// This prevents collision exploits where players could spawn inside walls
if (!IsValidSpawnPosition(spawnPoint))
{
    return false;
}
```

### Communication Pattern with Humans

When the human asks you to help:

1. **Acknowledge** what you're going to do
2. **Reference** the relevant `.docs/` files you're using as context
3. **Show progress** as you work (especially on large tasks)
4. **Provide clear next steps** when you're done

**Example response:**
```
I'll implement the player spawn system. I'm using:
- Reference: ARCHITECTURE.md - Player spawning section
- Reference: NETWORKING.md - Late-joiner synchronization

Here's what I'll create:
1. PlayerSpawner.cs - Manages spawn point selection and validation
2. SpawnPointManager.cs - Network synchronization of spawn points
3. Documentation in .docs/PLAYER_SPAWNING.md

Working on this now...
```

### Updating Tools and Scripts

When creating scripts in `.tools/`:

1. **Add header comments** explaining the script's purpose
2. **Include error handling** with clear messages
3. **Document parameters** if the script takes arguments
4. **Log actions** to `.tools/logs/` for debugging
5. **Test on both Windows and Unix** (or clearly document limitations)

**Script template:**
```powershell
<#
.SYNOPSIS
Brief description of what this script does

.DESCRIPTION
Detailed description

.PARAMETER Version
Description of any parameters

.EXAMPLE
.\script-name.ps1 -Version "2023.2.15f1"
#>

param(
    [string]$Version
)

# Implementation here
```

### When to Ask for Human Decision

Ask the human (don't decide yourself):
- **Game design questions** - How should a feature work?
- **Gameplay balance** - Is this difficulty/reward balanced?
- **Hardware-specific features** - Does this work on target platform?
- **Architecture conflicts** - Multiple approaches, which is best?
- **Project timeline** - Should we implement this or defer it?

---

## Collaboration Workflow

### Typical Feature Development Cycle

1. **Human proposes feature** using Pattern 1 above
2. **AI analyzes** existing `.docs/` files for context
3. **AI suggests** architecture using ARCHITECTURE.md as reference
4. **Human approves** (or iterates on) the design
5. **AI implements** the feature
6. **AI updates** or creates `.docs/[Feature].md`
7. **AI provides** human with summary and next steps
8. **Human reviews** code changes and tests
9. **Human commits** with approval

### Handling Disagreements

If human and AI have different ideas:
1. Document both approaches in the relevant `.md` file
2. Discuss pros/cons
3. **Human makes the final call** (this is their project)
4. Document the decision for future reference

---

## Documentation Structure

### `.docs/` Folder Organization

```
.docs/
├── README.md               # Index of all docs
├── ARCHITECTURE.md         # Overall system design
├── NETWORKING.md           # Networking system details
├── FPS_CORE.md             # First-person controller system
├── PLAYER_SPAWNING.md      # Player spawn system
├── [Feature].md            # One doc per major system
├── SETUP_AND_UPGRADES.md   # This file - environment setup
├── CONTRIBUTING.md         # This file - collaboration guide
└── AI_INTEGRATION.md       # (Future) How AI helps with development
```

### Updating Documentation

When code changes:
- **Small fixes** → Update inline comments
- **New feature** → Create new `.md` file + link from related docs
- **Architecture change** → Update ARCHITECTURE.md + relevant feature docs
- **Setup/version change** → Update SETUP_AND_UPGRADES.md

**Always keep docs in sync with code.** Out-of-date docs confuse future developers (including the AI).

---

## Quick Reference Checklist

### Before Submitting Work

- [ ] Code follows naming conventions (see Code Standards above)
- [ ] All public methods have `///` comments
- [ ] Relevant `.docs/` files are updated
- [ ] No hardcoded paths or version numbers (use config files)
- [ ] Commit message includes `[System]` prefix
- [ ] Related `.md` files are linked

### For AI-Specific Work

- [ ] `PROJECT_MANIFEST.md` references new docs (if applicable)
- [ ] Scripts in `.tools/` have error handling
- [ ] All changes logged (for human review)
- [ ] `.github/` workflows reference current Unity version

---

## Getting Help

### You're Stuck?

1. **Search `.docs/` files** for similar scenarios
2. **Check ARCHITECTURE.md** for system design guidance
3. **Ask the AI assistant** with reference to relevant `.md` files
4. **Check git history** for when similar code was added

### The AI is Confused?

Provide:
- **Exact error message** (copy-paste from console)
- **Reference to relevant `.md` files** you're using
- **Context** about what you're trying to do
- **Steps to reproduce** if it's a bug

---

## Version Control

### Branch Naming

```
feature/[system]/[description]     # New feature
bugfix/[system]/[description]      # Bug fix
docs/[area]                        # Documentation updates
ci/[description]                   # CI/CD changes
```

**Examples:**
```
feature/networking/player-spawn-sync
bugfix/fps/camera-jitter
docs/architecture-network-flow
ci/add-unity-build-workflow
```

### Before Pushing

```powershell
# Ensure no hardcoded paths
grep -r "C:\" Assets/Scripts/
grep -r "D:\" Assets/Scripts/

# Ensure no debug code
grep -r "Debug.Log" Assets/Scripts/ | grep -v "// " | head -20

# Check formatting
# (Use your IDE's formatter)
```

---

## Next Steps

1. ✅ Read this entire document
2. ✅ Review [PROJECT_MANIFEST.md](./PROJECT_MANIFEST.md)
3. ✅ Review [SETUP_AND_UPGRADES.md](./SETUP_AND_UPGRADES.md)
4. ✅ Check out [ARCHITECTURE.md](./ARCHITECTURE.md) (when created)
5. ✅ Start your first task using Pattern 1 above

---

**Remember:** This project is a partnership between humans and AI. Clear communication and well-maintained documentation make that partnership successful.
