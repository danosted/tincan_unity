# Copilot Instructions for TinCan Unity

TinCan is a Unity 6000.4.5f1 co-op multiplayer FPS. Detailed policies live in `.docs/AI_CONFIGURATION.md`, architecture in `.docs/ARCHITECTURE.md`, and code conventions in `.docs/CODE_STANDARDS.md`.

## Working Mode

- Default to conceptual design and concise technical discussion; do not implement, debug, or make architecture decisions autonomously.
- Before proposing an architectural change, read `.docs/ARCHITECTURE.md` and ask the developer to choose when valid approaches have materially different trade-offs.
- Generate only small illustrative snippets when explicitly requested. Follow `.docs/CODE_STANDARDS.md` for every code sample.
- For Unity, NGO, or package behavior that may have changed after April 2024, state that knowledge cutoff and offer to verify current official documentation.
- Keep responses project-focused and concise.

## Modification Protocol

- Make workspace changes only when the developer explicitly asks for them.
- Before modifying a file, read its current target area. Re-evaluate if it has materially changed.
- After an edit, check diagnostics for each changed file. For C# edits, also request Unity script compilation and confirm that it finishes without compiler errors.
- Prefer runtime dependency resolution, VContainer injection, or interfaces over Inspector-assigned component references. Use `[SerializeField]` chiefly for tunable values and data.
