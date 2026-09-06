# Code Standards & Best Practices

The coding rules for the TinCan project. **All AI agents and human contributors must follow these rules.**
For where code goes, see [`CODE_MAP.md`](./CODE_MAP.md); for the design rules, [`ARCHITECTURE.md`](./ARCHITECTURE.md).

## 1. Asynchronous Programming
- **No Coroutines.** Unity Coroutines (`IEnumerator`) are not used in this project.
- **UniTask is the intended replacement, but it is not installed yet** and no async code exists in the repo.
  If you need async work, add the [UniTask](https://github.com/Cysharp/UniTask) package to
  `Packages/manifest.json` first (agree it with the team), then use `async UniTask` / `async UniTaskVoid`.
- **Cancellation:** Always pass and respect `CancellationToken`s in async methods to avoid leaks when GameObjects are destroyed.

## 2. Dependency Injection & References
- **Minimize `[SerializeField]`:** Do not drag-and-drop references in the Unity Inspector unless absolutely necessary (like setting up a UI prefab or a visual effect). `[SerializeField]` is fine for tunables, config assets, `InteractionDefinition`s and asset identities (tags, attributes).
- **Dynamic Resolution:** Prefer resolving dependencies programmatically via VContainer `[Inject]`, `GetComponent()`, or `GetComponentInChildren()` in `Awake`/`Start`/`OnNetworkSpawn`.
- **Constructor Injection:** For plain C# classes (UseCases, Processors, Handlers), use Constructor injection exclusively. If a class has a test-only constructor overload, mark the production constructor `[Inject]` (VContainer picks the longest one otherwise).

## 3. Naming Conventions
- **Interfaces:** Prefix with `I` (e.g., `IPossessable`).
- **Classes/Structs:** PascalCase (e.g., `NetworkMediator`).
- **Private Fields:** Prefix with an underscore `_` and use camelCase (e.g., `_playerHealth`).
- **Public Properties:** PascalCase (e.g., `CurrentHealth`).
- **Constants/Statics:** PascalCase (e.g., `MaxPlayers`).
- **Suffixes** carry meaning: `*Processor`, `*UseCase`, `*NetworkMediator`, `*View`, `*Config`, `*Definition`, `*Handler`, `*FeatureInstaller`. The glossary in `CODE_MAP.md` defines each.

## 4. Nullable
- Every new C# file starts with `#nullable enable`. Use `?` on anything that can legitimately be null (unassigned `[SerializeField]`s, lookups that can fail) and guard it.

## 5. Networking Code (NGO)
- **Naming:** every `NetworkBehaviour` ends in `NetworkMediator`.
- **Server/Client Prefix:** Append `ServerRpc` or `ClientRpc` to the end of RPC method names as required by NGO.
- **Logic Separation:** Keep `NetworkBehaviour` classes thin. They should act merely as a transport layer that receives network events and passes data down to pure C# logic classes.
- **Guards:** every write to replicated state starts with an `IsServer` check.

## 6. Clean Code Rules
- **Keep it small:** Classes should have a single responsibility.
- **Fail Fast:** Use Guard clauses at the top of methods instead of deep nesting; prefer early returns over wrapping the remaining logic in nested `if` blocks.
- **Pattern Matching:** Prefer switch expressions (`x switch { ... }`) and pattern-matching switch statements over long if/else-if chains when branching on an enum or a small combination of conditions. Use tuple patterns (e.g. `switch (a, b) { case (true, false): ... }`) for multi-condition matrices, and `when` guards for conditional arms. One arm per case/condition.
- **Named Tuples:** Use named tuple elements (e.g. `(IActor Requester, IActor Target)`) instead of positional tuples when returning or destructuring multiple values, for clarity at call sites.
- **Regions:** Do not use `#region`. If a class is too large and needs regions, it should be refactored into multiple classes.

## 7. Tests
- Processors, use cases and interaction handlers ship with an EditMode test in `Assets/Tests/EditMode/`.
- Use the hand-written fakes in `Assets/Tests/EditMode/Fakes/`; add one when an interface has none. No mocking framework.
- Views keep their math in a `public static` function and test that.

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System design patterns
- [CODE_MAP.md](./CODE_MAP.md) - Where things live and what the suffixes mean
- [TASK_GUIDES.md](./TASK_GUIDES.md) - Recipes, including "Write a test or a fake"
