# Code Standards & Best Practices

**Document Version:** 2.0
**Last Updated:** April 23, 2026

This document outlines the strict coding standards required for the TinCan project. **All AI agents and human contributors must follow these rules.**

## 1. Asynchronous Programming (UniTask)
- **Use UniTask, not Coroutines:** Unity Coroutines (`IEnumerator`) are deprecated in this project. Use [UniTask](https://github.com/Cysharp/UniTask) for all asynchronous operations (`async UniTaskVoid`, `async UniTask`, etc.).
- **Cancellation:** Always pass and respect `CancellationToken`s when writing async methods to avoid memory leaks when GameObjects are destroyed.

## 2. Dependency Injection & References
- **Minimize `[SerializeField]`:** Do not drag-and-drop references in the Unity Inspector unless absolutely necessary (like setting up a UI prefab or a visual effect).
- **Dynamic Resolution:** Prefer resolving dependencies programmatically via VContainer `[Inject]`, `GetComponent()`, or `GetComponentInChildren()` in `Awake`/`Start`.
- **Constructor Injection:** For plain C# classes (UseCases, Processors), use Constructor injection exclusively.

## 3. Naming Conventions
- **Interfaces:** Prefix with `I` (e.g., `IPossessable`).
- **Classes/Structs:** PascalCase (e.g., `NetworkMediator`).
- **Private Fields:** Prefix with an underscore `_` and use camelCase (e.g., `_playerHealth`).
- **Public Properties:** PascalCase (e.g., `CurrentHealth`).
- **Constants/Statics:** PascalCase (e.g., `MaxPlayers`).

## 4. Networking Code (NGO)
- **Server/Client Prefix:** Append `ServerRpc` or `ClientRpc` to the end of RPC method names as required by NGO.
- **Logic Separation:** Keep `NetworkBehaviour` classes thin. They should act merely as a transport layer that receives network events and passes data down to pure C# logic classes.

## 5. Clean Code Rules
- **Keep it small:** Classes should have a single responsibility.
- **Fail Fast:** Use Guard clauses at the top of methods instead of deep nesting.
- **Regions:** Do not use `#region`. If a class is too large and needs regions, it should be refactored into multiple classes.

## Related Documents

- [CONTRIBUTING.md](./CONTRIBUTING.md) - Contribution guidelines
- [ARCHITECTURE.md](./ARCHITECTURE.md) - System design patterns

## Creation Trigger

This document will be fully written when:
1. Initial codebase is established
2. Code patterns emerge
3. Need for standardization becomes clear

**Check back after development begins for the full coding standards guide.**
