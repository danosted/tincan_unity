# Quick Reference Card

**TinCan Unity Project** | January 18, 2026

---

## 🚀 First Time Setup (2 minutes)

**Windows:**
```powershell
.\.tools\setup.ps1
```

**macOS/Linux:**
```bash
./.tools/setup.sh
```

Then open project in Unity Hub.

---

## 📚 Essential Reading (In Order)

1. **README.md** (5 min) - Project overview
2. **PROJECT_MANIFEST.md** (5 min) - Folder structure
3. **.docs/CONTRIBUTING.md** (10 min) - How to work with AI
4. **.docs/EXAMPLES.md** (10 min) - Real-world request examples
5. **.docs/SETUP_AND_UPGRADES.md** (5 min) - Version management & Git LFS

---

## 🤖 How to Ask AI for Help

**Pattern:**
```
Task: [What to build]
System: [Networking/FPS/UI/etc]
Requirements: [What you need]
Reference Docs: [.docs/*.md files]
```

**Example:**
```
Task: Add player movement
System: FPS
Requirements: WASD movement, Space to jump
Reference Docs: FPS_CORE.md, ARCHITECTURE.md
```

---

## 🔄 Version Management

**Check version:**
```powershell
cat .\.unity-version
```

**Upgrade:**
```powershell
.\.tools\upgrade-unity.ps1 -TargetVersion "2024.1.0f1"
```

---

## 📁 Project Structure

```
Assets/
├── Scripts/    ← Your code here
│   ├── Core/
│   ├── Network/
│   ├── Player/
│   ├── UI/
│   └── Utils/
├── Prefabs/    ← Reusable objects
├── Scenes/     ← Game levels
└── Resources/  ← Runtime assets

.docs/          ← Documentation hub
.tools/         ← Automation scripts
```

---

## 🔗 Documentation Map

| Need | File |
|------|------|
| Project overview | README.md |
| Setup/versions | .docs/SETUP_AND_UPGRADES.md |
| Working with AI | .docs/CONTRIBUTING.md |
| Architecture | .docs/ARCHITECTURE.md |
| Networking | .docs/NETWORKING.md |
| FPS system | .docs/FPS_CORE.md |
| **All docs** | **.docs/README.md** |

---

## ✨ Key Principles

✅ **One version file** → `.unity-version`  
✅ **Reference docs** → When asking AI for help  
✅ **AI maintains docs** → As code is written  
✅ **Humans decide** → Design & final approval  

---

## 🛠️ Common Commands

```powershell
# Setup
.\.tools\setup.ps1

# Upgrade
.\.tools\upgrade-unity.ps1 -TargetVersion "2024.1.0f1"

# Check version
cat .\.unity-version

# View logs
ls .\.tools\logs\ | tail -5

# Start developing
# → Ask AI to implement your feature!
```

---

## 🎯 Development Workflow

1. **You:** Design feature + create `.md` doc
2. **You:** Ask AI (reference the `.md`)
3. **AI:** Implement code + update docs
4. **You:** Review code + docs
5. **You:** Commit with doc reference

Repeat for each feature!

---

## ⚠️ If Something Goes Wrong

**Setup failed?**
→ Check `.tools/logs/setup-*.log`

**Can't find docs?**
→ Read `.docs/README.md`

**Version issues?**
→ Read `.docs/SETUP_AND_UPGRADES.md`

**Not sure about workflow?**
→ Read `.docs/CONTRIBUTING.md`

---

## 🎮 You're Ready!

- ✅ Run setup.ps1/sh
- ✅ Open in Unity
- ✅ Read docs
- ✅ Ask AI to build your first feature!

---

**Location:** `c:\Users\danos\source\repos\unity3d\tincan_unity`  
**Version:** 2023.2.15f1  
**Status:** Ready for Development
