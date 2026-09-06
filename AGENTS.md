# AI Agent Instructions — TinCan Unity

Single, vendor-agnostic instruction set for all AI assistants working on this repo (Claude, Copilot, Cursor, or anything else). Vendor-specific files (`.github/copilot-instructions.md`, and a `CLAUDE.md` if one is added) are thin pointers to this file — keep it that way so instructions never drift between tools.

TinCan is a Unity co-op multiplayer FPS (version pinned in `.unity-version`) built on Netcode for GameObjects and VContainer.

## Required reading (canonical sources — do not duplicate their content here)

1. [`.docs/AI_CONFIGURATION.md`](.docs/AI_CONFIGURATION.md) — how AI assistants must operate: discussion mode by default, implementation mode on request, when to ask the developer, concision rules, mandatory pre/post-modification checks.
2. [`.docs/ARCHITECTURE.md`](.docs/ARCHITECTURE.md) — core pillars: VContainer DI, NGO networking, input-driven simulation, possession and interaction, feature composition, and what happens from Play to the tick loop. Read before any architectural discussion or decision.
3. [`.docs/CODE_MAP.md`](.docs/CODE_MAP.md) — assemblies, folder map, naming glossary, "where does X live", the feature index, and known traps. Read before searching the codebase.
4. [`.docs/CODE_STANDARDS.md`](.docs/CODE_STANDARDS.md) — C# style, nullable, no coroutines, runtime resolution/injection over `[SerializeField]` component references, tests. Follow for every code sample.
5. [`.docs/TASK_GUIDES.md`](.docs/TASK_GUIDES.md) and [`.docs/TUTORIAL_NEW_FEATURE.md`](.docs/TUTORIAL_NEW_FEATURE.md) — the recipes and the build order to follow when implementing. Reference details in [`.docs/FEATURE_INSTALLERS.md`](.docs/FEATURE_INSTALLERS.md), [`.docs/UI_FRAMEWORK.md`](.docs/UI_FRAMEWORK.md), [`.docs/Network_Initialization_Flow.md`](.docs/Network_Initialization_Flow.md).

The human entry point is [`.docs/README.md`](.docs/README.md). Keep it and the docs above true: when you change how the project works, update the doc in the same change.

## Working mode

- Default to conceptual design and concise technical discussion; do not implement, debug, or make architecture decisions autonomously.
- Make workspace changes only when the developer explicitly asks for them. Then follow the implementation rules in `.docs/AI_CONFIGURATION.md` Rule 1.
- When valid approaches have materially different trade-offs, ask the developer to choose.
- For evolving Unity, NGO, package, or CLI behavior, verify against the installed tooling and current official documentation instead of remembered APIs.

## Modification protocol

- Before modifying a file, read its current target area; re-evaluate if it has materially changed.
- Any new `NetworkBehaviour` MUST be suffixed with `NetworkMediator` — never `Controller` or `Manager` for networked components.
- New features register through a `FeatureInstaller` asset under `Assets/Resources/Installers/`. Never add feature registrations or fields to `ProjectLifetimeScope.cs`, and do not edit the "Do NOT touch" files listed in `.docs/TUTORIAL_NEW_FEATURE.md`.
- Every processor, use case and interaction handler ships with an EditMode test; the suite stays green.
- A new feature gets a row in the feature index in `.docs/CODE_MAP.md`.
- After a C# edit, check diagnostics and request Unity script compilation; confirm it finishes without compiler errors.
- In documentation, cite file paths rather than pasting code.
- Finish implementation work with a human-readable summary: what changed, which files to read first, what to look at in the Editor, what still needs a human playtest.

## Unity Editor operations

- Use workspace file tools for source code and text files; never rewrite C# through an Editor command.
- Use the `unity` MCP server (or the `unity` CLI Pipeline commands — `unity status`, `unity cmd <tool>` — when MCP is unavailable) for live Editor state: Console logs, play mode, tests, scene inspection, and changes to GameObjects, components, prefabs, scenes, ScriptableObjects, materials, or project settings.
- Do not hand-edit serialized scene or prefab YAML when an Editor-aware operation is available.
- Before an object change, inspect current state; register changes for Undo, save affected scenes/assets explicitly, then verify via Console output or a scene capture.

## Project quick facts

- Fresh machine setup: `.\.tools\setup.cmd` (details in [`.tools/README.md`](.tools/README.md)). `.unity-version` is the version source of truth; `ProjectSettings/ProjectVersion.txt` is owned by the Editor.
- Scenes contain no camera. The camera lives on the player prefab and only exists after Play → Start Host spawns the player — "No cameras rendering" before that is expected, not a bug.
- Empty asset folders are kept in git via `.gitkeep` files (Unity ignores dot-files, so they get no `.meta`).
- Plans live in `.docs/plans/` and are committed with the work. Start each with a `Status: Draft | Approved | Done` line.
