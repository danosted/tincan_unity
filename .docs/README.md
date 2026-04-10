# Documentation Index

**Document Version:** 1.0
**Last Updated:** January 18, 2026

This folder contains all configuration, architecture, and collaboration documentation for the TinCan project. All documents are designed to be AI-maintainable and human-friendly.

## 🤖 For AI Assistants

**→ [AI_CONFIGURATION.md](./AI_CONFIGURATION.md) ← READ THIS FIRST**

Operational guidelines for AI on this project:
- How to verify current documentation (not cached knowledge)
- Pre-implementation checklist
- Knowledge cutoff handling (April 2024)
- When to ask the human for decisions
- Version verification requirements

Before any implementation, read this file!

---

## Essential Reading (Start Here)

### 1. [PROJECT_MANIFEST.md](../PROJECT_MANIFEST.md)
**What:** Project overview and folder structure philosophy
**When:** Read first - gives you the big picture
**Key Sections:**
- Project Overview
- Key Documents table
- Folder Structure Philosophy
- AI Integration Points

### 2. [SETUP_AND_UPGRADES.md](./SETUP_AND_UPGRADES.md)
**What:** Installation, version management, and troubleshooting
**When:** Read before setting up locally
**Key Sections:**
- Initial Project Setup
- One-Command Setup
- Upgrading Unity Versions
- Troubleshooting

### 3. [CONTRIBUTING.md](./CONTRIBUTING.md)
**What:** How to work with AI assistant and your team
**When:** Read before your first contribution
**Key Sections:**
- Working with AI Assistant (Patterns 1-3)
- Code Standards
- Commit Message Standards
- For AI Assistants (role and responsibilities)

---

## Architecture & Design

These documents describe how the game's major systems work together.

### [ARCHITECTURE.md](./ARCHITECTURE.md) *(Planned)*

**Purpose:** Overall system design and data flow
**Includes:**
- System components and their relationships
- Data flow diagrams
- Class hierarchy overview
- Design patterns used in this project

**When to read:** Before implementing any new system
**When to update:** When adding major features or refactoring

---

## Feature Systems

These documents detail implementation of major game systems.

### Networking

#### [NETWORKING.md](./NETWORKING.md) *(Planned)*

**Topics:**
- NetCode for GameObjects setup
- RPC (Remote Procedure Call) patterns
- NetworkTransform usage
- Synchronization strategies
- Connection lifecycle

**Read when:** Implementing any networked feature

#### [PLAYER_SPAWNING.md](./PLAYER_SPAWNING.md) *(Planned)*

**Topics:**
- Spawn point system
- Team-based spawning
- Late-joiner handling
- Spawn validation

**Read when:** Adding spawn-related features

### First-Person System

#### [FPS_CORE.md](./FPS_CORE.md) *(Planned)*

**Topics:**
- First-person camera setup
- Input handling
- Movement controller
- Combat system basics
- Animation integration

**Read when:** Building player movement or camera features

### User Interface

#### [UI_SYSTEM.md](./UI_SYSTEM.md) *(Planned)*

**Topics:**
- Menu navigation
- Multiplayer lobby
- HUD (heads-up display)
- Settings and configuration UI

**Read when:** Adding UI elements

---

## Configuration & Development

### [AI_INTEGRATION.md](./AI_INTEGRATION.md) *(Planned)*

**Purpose:** How the AI assistant integrates into your development workflow
**Includes:**
- When to ask the AI for help
- How to reference documentation in requests
- AI responsibilities and limitations
- Feedback loop for improving AI assistance

### [CODE_STANDARDS.md](./CODE_STANDARDS.md) *(Planned)*

**Purpose:** Code style, naming conventions, and best practices
**Includes:**
- C# style guide for this project
- Naming conventions (classes, methods, fields)
- Comment and documentation standards
- Performance guidelines
- Testing requirements

---

## Quick Navigation by Task

### "I want to..."

| Task | Read These Docs |
|------|-----------------|
| Set up the project locally | SETUP_AND_UPGRADES.md → Initial Project Setup |
| Understand the overall architecture | ARCHITECTURE.md → all sections |
| Add a new networked feature | NETWORKING.md → CONTRIBUTING.md Pattern 1 |
| Fix a bug in player movement | FPS_CORE.md → CONTRIBUTING.md Pattern 2 |
| Upgrade Unity version | SETUP_AND_UPGRADES.md → Upgrading Unity Versions |
| Understand the folder structure | PROJECT_MANIFEST.md → Folder Structure Philosophy |
| Work with the AI assistant | CONTRIBUTING.md → For Human Contributors → Working with AI Assistant |
| Write code for this project | CODE_STANDARDS.md → CONTRIBUTING.md → Code Standards |
| Review someone else's code | CODE_STANDARDS.md → CONTRIBUTING.md Pattern 3 |
| Add new documentation | CONTRIBUTING.md → Documentation Structure |

---

## New: Real-World Examples

**[EXAMPLES.md](./EXAMPLES.md)** is your go-to reference for:
- Example 1: Implementing a feature (player spawn system)
- Example 2: Fixing a bug (sync issues)
- Example 3: Code review request
- Example 4: Architecture consultation
- Example 5: Refactoring request
- Example 6: Documentation update
- Example 7: Performance optimization

Read this before asking the AI for help!

## Document Status Legend

| Status | Meaning |
|--------|---------|
| ✅ Active | Actively maintained, safe to follow |
| 🔄 Planned | Will be created as features are built |
| 📝 Draft | Exists but may change frequently |
| ⚠️ Outdated | Needs review and update |

---

## How These Docs Are Maintained

### For Humans

- Update documentation when requirements change
- Flag docs with `⚠️ OUTDATED` comment if information becomes incorrect
- Reference relevant docs in your git commits
- Ask the AI assistant to update docs when features change

### For AI Assistants

- Review relevant `.md` files before implementing features
- Keep docs in sync with code changes
- Create new `.md` files for new systems
- Link new docs from parent docs and PROJECT_MANIFEST.md
- Update this README.md index when creating new documents

### Update Cycle

1. **Code is implemented** (by AI, following relevant `.md` guides)
2. **Docs are created/updated** (AI maintains in `.docs/` folder)
3. **Human reviews** code + docs together
4. **Human approves and commits** (with reference to docs in commit message)
5. **Docs become source of truth** for next feature

---

## Common Documentation Patterns

### When Creating a New System Doc

Use this template:

```markdown
# [System Name] System

**Document Version:** 1.0
**Last Updated:** [Date]
**Maintainer:** AI Assistant + [Human Lead]

## Overview
[What this system does, why it exists]

## Architecture
[How it's structured, relationships to other systems]

## Key Classes
[List of main classes and brief descriptions]

## Usage Examples
[Code showing how to use this system]

## Configuration
[Parameters that can be tweaked]

## Integration Points
[How other systems interact with this]

## Known Limitations
[Current constraints, TODOs]

## Related Docs
- [Link to parent doc]
- [Link to related system]
```

### When Updating Existing Docs

1. Update the "Last Updated" date at the top
2. Add changelog entry if there are significant changes
3. Update related doc links if structure changed
4. Commit with message: `docs: [System] - [brief description]`

---

## Feedback Loop for Improvement

As you use these docs, note:
- ❌ **Missing Information:** What couldn't you find?
- ❌ **Unclear Sections:** What confused you?
- ✅ **What Worked Well:** What was helpful?

Share feedback with your AI assistant using:
```
Doc Feedback: [Doc Name]
Issue: [What was confusing]
Suggestion: [How to improve]
```

The AI assistant will update docs based on this feedback.

---

## Search Tips

Looking for something? Try searching in your editor for:

- **`Planned`** - To find all docs that will be created
- **`Reference:`** - To find cross-references in docs
- **`AI Integration`** - To find sections about AI assistance
- **`Code Example`** - To find usage examples

---

## Next Steps

1. ✅ You're reading this file
2. ✅ Open [PROJECT_MANIFEST.md](../PROJECT_MANIFEST.md)
3. ✅ Open [SETUP_AND_UPGRADES.md](./SETUP_AND_UPGRADES.md)
4. ✅ Run the setup script from your OS
5. ✅ Come back and read [CONTRIBUTING.md](./CONTRIBUTING.md)

---

**Last Updated:** January 18, 2026
**Questions?** Reference the relevant doc section in your question.
