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
- Before declaring any body of work involving project source or assets complete, run the applicable Unity test suites using the commands under **Unity Editor Operations** and confirm they pass. EditMode tests are mandatory for code changes; PlayMode tests are additionally mandatory when runtime behavior is affected. If tests cannot be run, report the blocker and do not describe the work as fully verified.

## Unity Editor Operations

- Use workspace file tools for source code and text files. Do not use an Editor command to rewrite C# source.
- Prefer the `unity` MCP server for live Editor state, Console logs, tests, Scene inspection, and changes to GameObjects, components, prefabs, scenes, ScriptableObjects, materials, or project settings.
- When the interactive Editor is open, run EditMode tests with `unity command run_tests`. Run PlayMode tests with `unity command run_tests --mode playmode --async_tests`, then poll completion with `unity command test_status`. Use the standalone `unity test` command only for CI or when the project is not already open.
- Before an MCP object change, inspect the current state. Register creations, modifications, and deletions with the command result for Undo; explicitly save affected scenes or assets; then verify Console output and capture the Scene view when visual placement matters.
- Do not hand-edit serialized scene or prefab YAML when an Editor-aware MCP operation is available.
- Prefer dedicated MCP compilation and status tools when exposed. If MCP is unavailable or disconnects during domain reload, use the `unity` CLI Pipeline commands and report when no Editor is reachable.

## Project quick facts

- Fresh machine setup: `.\.tools\setup.cmd` (details in [`.tools/README.md`](.tools/README.md)). `.unity-version` is the version source of truth; `ProjectSettings/ProjectVersion.txt` is owned by the Editor.
- Scenes contain no camera. The camera lives on the player prefab and only exists after Play → Start Host spawns the player — "No cameras rendering" before that is expected, not a bug.
- Empty asset folders are kept in git via `.gitkeep` files (Unity ignores dot-files, so they get no `.meta`).
