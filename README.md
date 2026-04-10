# TinCan Unity - Project Root README

Welcome to TinCan, a co-op multiplayer first-person game built with Unity3D and AI-assisted development practices.

## 🚀 Quick Start

### First Time Setup

**Windows:**
```powershell
.\.tools\setup.ps1
```

**macOS/Linux:**
```bash
./.tools/setup.sh
```

This will:
- Validate folder structure
- Check Unity version from `.unity-version` file
- Configure required packages
- Create environment files

### Next Steps

1. **Review Documentation:**
   - [PROJECT_MANIFEST.md](./PROJECT_MANIFEST.md) - Project overview & structure
   - [.docs/README.md](./.docs/README.md) - Documentation index
   - [.docs/CONTRIBUTING.md](./.docs/CONTRIBUTING.md) - How to contribute

2. **Open in Unity:**
   - Open Unity Editor
   - File → Open Project
   - Select this folder
   - Unity will initialize the project

3. **Read the Contributing Guide:**
   - Understand how to work with the AI assistant
   - Learn code standards
   - See collaboration patterns

## 📚 Documentation Structure

```
.docs/
├── README.md                    # Documentation index
├── EXAMPLES.md                  # Real-world AI request examples
├── PROJECT_MANIFEST.md          # Project overview (at root)
├── SETUP_AND_UPGRADES.md       # Version management & Git LFS
├── CONTRIBUTING.md              # Collaboration guide
├── ARCHITECTURE.md              # System design (planned)
├── NETWORKING.md                # Networking details (planned)
├── FPS_CORE.md                  # First-person system (planned)
├── PLAYER_SPAWNING.md           # Spawn system (planned)
├── CODE_STANDARDS.md            # Code style guide (planned)
└── AI_INTEGRATION.md            # AI assistance guide (planned)
```

## 🛠️ Tools & Automation

`.tools/` folder contains scripts for:
- **setup.ps1 / setup.sh** - Project initialization
- **upgrade-unity.ps1 / upgrade-unity.sh** - Safe version upgrades
- **logs/** - Timestamped logs from all scripts

See [.tools/README.md](./.tools/README.md) for details.

## ⚙️ Project Configuration

### Unity Version

The project uses **a single source of truth for the Unity version:**

**File:** `.unity-version`

Contains only the version string (e.g., `2023.2.15f1`). All version updates go through the upgrade script, which automatically updates all related files.

### Packages

Core packages for this project:
- `com.unity.netcode.gameobjects` - Multiplayer networking
- `com.unity.transport` - Low-level networking
- `com.unity.burst` - Performance optimization
- `com.unity.collections` - Advanced data structures

Managed in: `Packages/manifest.json`

## 📁 Folder Structure

```
tincan_unity/
├── Assets/
│   ├── Scripts/          # Game code organized by system
│   │   ├── Core/
│   │   ├── Network/
│   │   ├── Player/
│   │   ├── UI/
│   │   └── Utils/
│   ├── Prefabs/          # Reusable game objects
│   ├── Scenes/           # Game levels and menus
│   └── Resources/        # Runtime-loaded assets
├── Packages/             # Package configuration
├── ProjectSettings/      # Unity project settings
├── .docs/                # AI-maintained documentation
├── .tools/               # Automation scripts
├── .github/              # CI/CD workflows (planned)
├── .gitignore            # Git ignore rules
├── .unity-version        # Single source of truth for version
└── PROJECT_MANIFEST.md   # Project overview
```

## � AI Integration Notes

This project includes **[.docs/AI_CONFIGURATION.md](./.docs/AI_CONFIGURATION.md)** which defines how the AI assistant should operate:

- ✅ Always checks current documentation (not cached knowledge)
- ✅ Verifies package compatibility before implementation
- ✅ References official sources in all decisions
- ✅ Asks when uncertain about changes since April 2024
- ✅ Includes pre-implementation checklist for every task

**If you're the AI:** Read AI_CONFIGURATION.md before starting any work.

- How to work with the AI assistant
- Code standards and naming conventions
- Commit message patterns
- Documentation expectations
- Collaboration workflows

### For AI Assistants

You are welcome as a development partner! See [.docs/CONTRIBUTING.md](./.docs/CONTRIBUTING.md#for-ai-assistants) for:
- Your role and responsibilities
- Code you should/shouldn't write
- How to maintain documentation
- Communication patterns

## 🎮 Project Goals

**TinCan** is a co-op multiplayer first-person game featuring:
- ✅ Network-synchronized multiplayer (2-8 players)
- ✅ First-person view and controls
- ✅ Robust version management for easy upgrades
- ✅ AI-integrated development workflow
- ✅ Clean, well-documented architecture

## 🔄 Upgrade Path

To upgrade to a new Unity version:

```powershell
.\.tools\upgrade-unity.ps1 -TargetVersion "2024.1.0f1"
```

See [SETUP_AND_UPGRADES.md](./.docs/SETUP_AND_UPGRADES.md) for full details.

## 📊 Project Status

- **Phase:** Early Development / Architecture
- **Last Updated:** January 18, 2026
- **Development Model:** Human + AI Collaboration
- **Primary Focus:** Foundation & Setup

## ❓ Getting Help

1. **Check the docs** - Most questions answered in [.docs/README.md](./.docs/README.md)
2. **Review examples** - Look at CONTRIBUTING.md for patterns
3. **Ask the AI** - Reference relevant `.md` files in your questions
4. **Check logs** - Review `.tools/logs/` if setup/upgrade fails

## 📋 Checklist for New Developers

- [ ] Run `.tools/setup.ps1` or `.tools/setup.sh`
- [ ] Read [PROJECT_MANIFEST.md](./PROJECT_MANIFEST.md)
- [ ] Read [.docs/README.md](./.docs/README.md)
- [ ] Read [.docs/CONTRIBUTING.md](./.docs/CONTRIBUTING.md)
- [ ] Review [.docs/SETUP_AND_UPGRADES.md](./.docs/SETUP_AND_UPGRADES.md)
- [ ] Open project in Unity Editor
- [ ] Verify all scenes load without errors
- [ ] Ask any questions referencing relevant documentation

## 📝 License

[Add your license here]

## 👥 Contributors

- Human Developer(s): [Your name(s)]
- AI Assistant: GitHub Copilot (Claude Haiku 4.5)

---

**Ready to start?** Run the setup script, then read [.docs/CONTRIBUTING.md](./.docs/CONTRIBUTING.md) to begin developing!
