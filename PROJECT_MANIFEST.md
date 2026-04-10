# TinCan Unity - Project Manifest

**Last Updated:** January 18, 2026  
**AI Assistant Integration:** Active  
**Project Status:** Early Development

## Project Overview

TinCan is a co-op multiplayer first-person game built with Unity3D using modern networking and AI-assisted development practices. This manifest serves as the single source of truth for the project structure, configuration, and development guidelines.

## Quick Reference

- **Engine Version:** Will be specified in `.unity-version`
- **Networking Stack:** Unity NetCode for GameObjects
- **Target Platforms:** PC, Consoles (planned)
- **Development Team:** Human + AI Contributors

## Key Documents

Each system should have corresponding documentation in [`.docs`](./.docs/) folder:

| System | Doc | Status |
|--------|-----|--------|
| Architecture | [ARCHITECTURE.md](./.docs/ARCHITECTURE.md) | Planned |
| Networking | [NETWORKING.md](./.docs/NETWORKING.md) | Planned |
| FPS Core | [FPS_CORE.md](./.docs/FPS_CORE.md) | Planned |
| AI Integration | [AI_INTEGRATION.md](./.docs/AI_INTEGRATION.md) | Planned |
| Setup & Upgrades | [SETUP_AND_UPGRADES.md](./.docs/SETUP_AND_UPGRADES.md) | Active |
| Contributing | [CONTRIBUTING.md](./.docs/CONTRIBUTING.md) | Active |

## Folder Structure Philosophy

```
tincan_unity/
├── .docs/               # AI-generated and maintained documentation
├── .tools/              # Automation scripts for setup and upgrades
├── .github/             # GitHub workflows and templates
├── Assets/              # All Unity game assets
│   ├── Scripts/         # Organized by system/feature
│   ├── Prefabs/         # Reusable game objects
│   ├── Scenes/          # Game levels and menus
│   └── Resources/       # Runtime-loaded assets
├── Packages/            # Custom and imported packages
├── ProjectSettings/     # Unity project configuration
└── PROJECT_MANIFEST.md  # This file
```

### `.docs/` Folder - AI Collaboration Hub

This folder contains all documentation files that will evolve as you and your AI assistant work together. Structure:

- **Architecture Docs**: System design, data flow, class diagrams
- **Feature Docs**: Detailed guides for each major system
- **Setup Docs**: Installation, version management, environment setup
- **Guidelines**: Code standards, contribution rules, AI usage patterns

### `.tools/` Folder - Automation Scripts

Scripts for:
- **Unity Version Management**: Automated upgrades with compatibility checks
- **Project Initialization**: One-command project setup
- **CI/CD Support**: Build and test automation

## AI Integration Points

The following locations are designated for AI assistant involvement:

1. **`.docs/` folder** - AI maintains and expands documentation
2. **Configuration files** - `.tools` scripts reference config docs
3. **Code architecture** - Following patterns documented in ARCHITECTURE.md
4. **Feature implementation** - Using guidelines in feature-specific .md files

To invoke AI assistance on any system, reference the corresponding `.md` file in `.docs/`.

## Unity Version Management

Instead of hard-coding version numbers throughout the project, we use:

- **`.unity-version`** file (root) - Single source of truth
- **Version upgrade scripts** in `.tools/` - Automate compatibility updates
- **ProjectSettings/ProjectVersion.txt** - Synced by upgrade script

## Getting Started

1. Run setup script: `.\\.tools\\setup.ps1` (Windows) or `./.tools/setup.sh` (Unix)
2. Review [SETUP_AND_UPGRADES.md](./.docs/SETUP_AND_UPGRADES.md)
3. Read [CONTRIBUTING.md](./.docs/CONTRIBUTING.md) to understand AI collaboration

## Key Principles

✅ **Documentation as Code** - All system architecture documented in `.md` files  
✅ **AI-Friendly Structure** - Clear separation of concerns for AI analysis  
✅ **Version Agnostic** - Easy Unity version upgrades via scripts  
✅ **Contributor-Ready** - Clear guidelines for human and AI contributors  
✅ **Automated Setup** - Minimal manual configuration needed  

## Next Steps

The following are prepared for your development:
- [ ] Create specific `.md` files in `.docs/` as features are designed
- [ ] Populate `.tools/` scripts with version management automation
- [ ] Add `.github/` workflows for CI/CD
- [ ] Initialize Unity project with manifests

---

**For detailed setup and upgrade instructions, see [SETUP_AND_UPGRADES.md](./.docs/SETUP_AND_UPGRADES.md)**
