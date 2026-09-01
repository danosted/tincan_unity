---
name: "TinCan Unity C#"
description: "Use when discussing, creating, or modifying TinCan Unity C# scripts, MonoBehaviours, VContainer dependencies, Netcode for GameObjects, simulations, abilities, possession, or interactions."
applyTo: "Assets/**/*.cs"
---
# TinCan Unity C# Rules

Read `.docs/ARCHITECTURE.md` and `.docs/CODE_STANDARDS.md` before recommending a design or generating C#.

- Keep `NetworkBehaviour` classes thin transport adapters and name every new one with the `NetworkMediator` suffix.
- Put game logic, calculations, timers, and state transitions in plain C# UseCases or Processors; inject their dependencies through constructors.
- Use input-driven simulation for movement, abilities, and gunplay. Represent time-critical player intent in `InputState` and run shared simulation logic for prediction and authority.
- Do not use independent `ServerRpc` calls to trigger simulated actions. Reserve RPCs for discrete, non-simulated events; use `NetworkVariable` or `ClientRpc` for state-driven synchronization and visual confirmation.
- Let simulation-owning UseCases propagate their predicted input to auxiliary simulation systems in the same tick. Global systems must skip actors that perform their own prediction.
- Use VContainer injection or dynamic component lookup instead of Inspector references wherever practical. Use UniTask with a `CancellationToken` for asynchronous work; do not introduce Coroutines.
- Use guard clauses, no `#region` blocks, underscore-prefixed camelCase private fields, PascalCase public members, and `I`-prefixed interfaces.
- Prefer switch expressions and pattern-matching switches (tuple patterns, `when` guards) over if/else-if chains when branching on an enum or a small combination of conditions — one arm per case. Prefer early returns over nested conditionals. Prefer named tuples over positional tuples when returning or destructuring multiple values.
- Use #nullable enable at the top of every C# file to enforce nullable reference type checks.

After editing C#, check diagnostics and confirm that Unity finishes compiling the project assemblies without errors. Prefer dedicated Unity MCP compilation and status tools when they are exposed; generic command-script compilation alone is not proof that the project assemblies compiled. If MCP is unavailable or disconnects during domain reload, use `unity command eval '...RequestScriptCompilation...'` followed by `unity command recompile_status`, and use `unity pipeline list` if the Editor connection cannot be rediscovered. Use MCP Console tools for the resulting errors and stack traces when the connection is available.
