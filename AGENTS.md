# AI Agent Instructions — TinCan Unity

Single, vendor-agnostic instruction set for all AI assistants working on this repo (Claude, Copilot, Cursor, or anything else). Vendor-specific files (`CLAUDE.md`, `.github/copilot-instructions.md`) are thin pointers to this file — keep it that way so instructions never drift between tools.

TinCan is a Unity co-op multiplayer FPS (version pinned in `.unity-version`) built on Netcode for GameObjects and VContainer.

## Required reading (canonical sources — do not duplicate their content here)

1. [`.docs/AI_CONFIGURATION.md`](.docs/AI_CONFIGURATION.md) — how AI assistants must operate: conceptual-discussion-first working mode, when to ask the developer, concision rules, code-generation preferences, mandatory pre/post-modification checks.
2. [`.docs/ARCHITECTURE.md`](.docs/ARCHITECTURE.md) — core pillars: VContainer DI, NGO networking, Possession & Interaction flow. Read before any architectural discussion or decision.
3. [`.docs/CODE_STANDARDS.md`](.docs/CODE_STANDARDS.md) — C# style, UniTask over coroutines, runtime resolution/injection over `[SerializeField]` component references. Follow for every code sample.
4. [`.docs/Network_Initialization_Flow.md`](.docs/Network_Initialization_Flow.md) — how VContainer and NGO initialize across host, dedicated server, and client.

## Working mode

- Default to conceptual design and concise technical discussion; do not implement, debug, or make architecture decisions autonomously.
- Make workspace changes only when the developer explicitly asks for them.
- Generate only small illustrative snippets when explicitly requested.
- When valid approaches have materially different trade-offs, ask the developer to choose.
- For evolving Unity, NGO, package, or CLI behavior, verify against the installed tooling and current official documentation instead of remembered APIs.

## Modification protocol

- Before modifying a file, read its current target area; re-evaluate if it has materially changed.
- Any new `NetworkBehaviour` MUST be suffixed with `NetworkMediator` — never `Controller` or `Manager` for networked components.
- After a C# edit, check diagnostics and request Unity script compilation; confirm it finishes without compiler errors.

## Unity Editor operations

- Use workspace file tools for source code and text files; never rewrite C# through an Editor command.
- Use the `unity` MCP server (or the `unity` CLI Pipeline commands — `unity status`, `unity cmd <tool>` — when MCP is unavailable) for live Editor state: Console logs, play mode, tests, scene inspection, and changes to GameObjects, components, prefabs, scenes, ScriptableObjects, materials, or project settings.
- Do not hand-edit serialized scene or prefab YAML when an Editor-aware operation is available.
- Before an object change, inspect current state; register changes for Undo, save affected scenes/assets explicitly, then verify via Console output or a scene capture.

## Project quick facts

- Fresh machine setup: `.\.tools\setup.cmd` (details in [`.tools/README.md`](.tools/README.md)). `.unity-version` is the version source of truth; `ProjectSettings/ProjectVersion.txt` is owned by the Editor.
- Scenes contain no camera. The camera lives on the player prefab and only exists after Play → Start Host spawns the player — "No cameras rendering" before that is expected, not a bug.
- Empty asset folders are kept in git via `.gitkeep` files (Unity ignores dot-files, so they get no `.meta`).
