# TinCan - Co-op Multiplayer on a Boat!

Welcome to TinCan! This is a Unity3D multiplayer game built with Netcode for GameObjects (NGO) and VContainer for dependency injection.

This project is developed using **Human + AI Collaboration**. We heavily rely on AI agents to help write code, but humans guide the architecture and decisions.

## 🚀 Quick Start

1. **Clone the Repo & Setup:**
   Run the setup script to validate your environment and install required packages.
   ```powershell
   .\.tools\setup.ps1  # Windows
   ./.tools/setup.sh   # macOS/Linux
   ```
2. **Open in Unity:** Open this folder as a Unity Project.
3. **Read the AI Rules:** Since you'll be working with AI, please review the AI Guidelines below.

## 🤖 AI Guidelines & Core Documentation

To ensure AI assistants generate high-quality code that matches our project's architecture, we maintain strict documentation inside the `.docs/` folder. **Both humans and AI agents must read these files:**

1. **[AI Configuration & Rules](.docs/AI_CONFIGURATION.md)**
   Defines *how* AI agents should interact with you (e.g., being concise, avoiding heavy code generation without permission, and verifying API usage).
2. **[Architecture Overview](.docs/ARCHITECTURE.md)**
   Explains the core pillars of our game: `VContainer` for Dependency Injection, `NGO` for networking, and the `Possession & Interaction` flow.
3. **[Code Standards](.docs/CODE_STANDARDS.md)**
   Specific rules on C# styling, using `UniTask` instead of coroutines, and avoiding heavy `[SerializeField]` usage in favor of dynamic resolution.

*Note: AI assistants are explicitly instructed via `.github/copilot-instructions.md` to reference the `.docs` folder before writing any code.*
