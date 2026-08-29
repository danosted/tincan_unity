# Copilot Instructions for TinCan Unity

TinCan is a Unity 6000.4.5f1 co-op multiplayer FPS. Detailed policies live in `.docs/AI_CONFIGURATION.md`, architecture in `.docs/ARCHITECTURE.md`, and code conventions in `.docs/CODE_STANDARDS.md`.

## Working Mode

- Default to conceptual design and concise technical discussion; do not implement, debug, or make architecture decisions autonomously.
- Before proposing an architectural change, read `.docs/ARCHITECTURE.md` and ask the developer to choose when valid approaches have materially different trade-offs.
- Generate only small illustrative snippets when explicitly requested. Follow `.docs/CODE_STANDARDS.md` for every code sample.
- For evolving Unity, NGO, package, or CLI behavior, verify the installed tooling and current official documentation instead of relying on remembered APIs.
- Keep responses project-focused and concise.

## Modification Protocol

- Make workspace changes only when the developer explicitly asks for them.
- Before modifying a file, read its current target area. Re-evaluate if it has materially changed.
- After an edit, check diagnostics for each changed file. For C# edits, also request Unity script compilation and confirm that it finishes without compiler errors.
- Prefer runtime dependency resolution, VContainer injection, or interfaces over Inspector-assigned component references. Use `[SerializeField]` chiefly for tunable values and data.

## Unity Editor Operations

- Use workspace file tools for source code and text files. Do not use an Editor command to rewrite C# source.
- Prefer the `unity` MCP server for live Editor state, Console logs, tests, Scene inspection, and changes to GameObjects, components, prefabs, scenes, ScriptableObjects, materials, or project settings.
- Before an MCP object change, inspect the current state. Register creations, modifications, and deletions with the command result for Undo; explicitly save affected scenes or assets; then verify Console output and capture the Scene view when visual placement matters.
- Do not hand-edit serialized scene or prefab YAML when an Editor-aware MCP operation is available.
- Prefer dedicated MCP compilation and status tools when exposed. If MCP is unavailable or disconnects during domain reload, use the `unity` CLI Pipeline commands and report when no Editor is reachable.
