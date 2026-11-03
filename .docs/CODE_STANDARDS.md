# Code Standards & Best Practices

The coding rules for the TinCan project. **All AI agents and human contributors must follow these rules.**
For where code goes, see [`CODE_MAP.md`](./CODE_MAP.md); for the design rules, [`ARCHITECTURE.md`](./ARCHITECTURE.md).

## 1. Asynchronous Programming
- **No Coroutines.** Unity Coroutines (`IEnumerator`) are not used in this project.
- **UniTask** ([`com.cysharp.unitask`](https://github.com/Cysharp/UniTask), from OpenUPM) is the async library. An
  assembly that uses it lists `UniTask` in its asmdef references. Use `async UniTask` / `UniTask<T>`, never `Task`, in
  game code.
- **`UniTaskVoid` only at the edge:** a fire-and-forget start (a menu command, a bootstrap) is a private
  `async UniTaskVoid` called with `.Forget()`, and it handles the result itself. Example: `JoinGameMenuCommand`.
- **Cancellation:** every method returning `UniTask` takes a `CancellationToken` and passes it on
  (`ArchitectureRulesTests.AsyncMethods_TakeACancellationToken`). A long-lived owner cancels its own token on
  `Dispose` (VContainer disposes singletons).
- **Tests** can be `async Task` (Unity Test Framework) and `await` a UniTask. Fakes complete synchronously, so nothing
  waits on the player loop. Example: `DirectSessionServiceEndTests`.

## 2. Dependency Injection & References
- **Minimize `[SerializeField]`:** Do not drag-and-drop references in the Unity Inspector unless absolutely necessary (like setting up a UI prefab or a visual effect). `[SerializeField]` is fine for tunables, config assets, `InteractionDefinition`s and asset identities (tags, attributes).
- **Dynamic Resolution:** Prefer resolving dependencies programmatically via VContainer `[Inject]`, `GetComponent()`, or `GetComponentInChildren()` in `Awake`/`Start`/`OnNetworkSpawn`.
- **Constructor Injection:** For plain C# classes (UseCases, Processors, Handlers), use Constructor injection exclusively. If a class has a test-only constructor overload, mark the production constructor `[Inject]` (VContainer picks the longest one otherwise).

### Assemblies: every feature is its own
- A new feature gets its own asmdef, `TinCan.Features.<Name>`, in its folder. It references only what it uses:
  `TinCan.Core.Domain`, the core systems it uses (`TinCan.Gas`, `TinCan.Interaction`, ...), the packages it needs,
  and the other features it builds on.
- Core never references a feature. When core needs something a feature provides, put the contract in
  `Core.Domain` and let the feature implement it.
- Worked example: `Features/GasChallenge/`. The rule, diagram and rule tests are in
  [CODE_MAP.md, "Assemblies and the one-way rule"](./CODE_MAP.md#assemblies-and-the-one-way-rule).

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
- **One type per file:** Each class, interface, struct and enum gets its own file, named after the type. The only exception is a type fully isolated to its containing class (a private nested type, or one used nowhere else). Older files break this rule: do not sweep them, but split a file when you touch it anyway, as part of that change.
- **Regions:** Do not use `#region`. If a class is too large and needs regions, it should be refactored into multiple classes.

## 7. Tests
- Processors, use cases and interaction handlers ship with an EditMode test.
- **Each assembly has its own test assembly, beside it.** The tests of `X` live in `X.Tests`: a `Tests/` folder next to
  `X`'s asmdef, holding `X.Tests.asmdef` (Editor only, define constraint `UNITY_INCLUDE_TESTS`) and namespace
  `<X's namespace>.Tests`. Example: `Features/Shipyard/Tests/TinCan.Features.Shipyard.Tests.asmdef`. A new feature
  creates its test assembly with its asmdef; copy one from a feature of the same shape.
- **A test assembly follows the one-way rule.** `X.Tests` references `X`, what `X` builds on, any core system (`Core/*`),
  and the shared fakes; never a feature `X` does not build on. A test that combines features neither builds on lives in
  `TinCan.Tests.Integration` (`Assets/Tests/Integration/`); whole-project rule tests live in `TinCan.Tests.Architecture`.
- **Fakes live with the interface they fake.** A fake of a core interface goes in `TinCan.Tests.Fakes`
  (`Assets/Tests/Fakes/`); a fake of a feature's interface goes in that feature's test assembly, under `Tests/Fakes/`.
  Use the hand-written fakes; add one when an interface has none. No mocking framework.
- Views keep their math in a `public static` function and test that.
- Run part of the suite with `.\.tools\verify.ps1 -UpTo Tests -TestAssembly <X.Tests>[,<Y.Tests>]`.
- `Assets/Tests/Architecture/ArchitectureRulesTests.cs` checks the rules that can be checked: naming, global lookups in
  `Features`, registrations in `ProjectLifetimeScope`, a test per processor/use case/handler, `#nullable enable`, no
  `#region` or coroutines, the assembly rules (a feature in its own assembly, core never referencing a feature,
  profiles loading the features their features reference) and the test assembly rules above. Known offenders sit in
  `ArchitectureRulesBaseline.cs`. Remove an entry when you fix it; never add one without the developer's agreement.

## Related Documents

- [ARCHITECTURE.md](./ARCHITECTURE.md) - System design patterns
- [CODE_MAP.md](./CODE_MAP.md) - Where things live and what the suffixes mean
- [TASK_GUIDES.md](./TASK_GUIDES.md) - Recipes, including "Write a test or a fake"
