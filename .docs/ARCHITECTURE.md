# Architecture Overview

**Document Version:** 2.0
**Last Updated:** April 23, 2026
**Maintainer:** AI Assistant + Lead Architect

## Core Pillars

TinCan is built on three core technical pillars designed to make multiplayer development scalable and modular:

### 1. Dependency Injection (VContainer)
We use [VContainer](https://vcontainer.hadashikick.jp/) as our DI framework.
- **No Singletons:** Avoid using `Instance` patterns. Inject dependencies via constructors or standard VContainer `[Inject]` attributes on MonoBehaviours.
- **ProjectLifetimeScope:** Found in `Core/Infrastructure/ProjectLifetimeScope.cs`. This is the composition root where services, UseCases, and NGO networking services are bound.
- **Entry Points:** We heavily utilize `IInitializable`, `ITickable`, and UseCases bound via `builder.UseEntryPoints(...)`.

### 2. Networking (Netcode for GameObjects - NGO)
All multiplayer synchronization runs through Unity's official Netcode for GameObjects.
- **NetworkMediators:** MonoBehaviours that inherit from `NetworkBehaviour` act as the authoritative bridge. Logic should be separated into pure C# `UseCase` or `Processor` classes, while the Mediator handles the RPCs and `NetworkVariables`.
- **Prefab Spawning:** Controlled strictly via VContainer's `AddNetworkedPrefab` extensions. See `ProjectLifetimeScope` for examples of how we spawn the player and airship.

### 3. Possession & Interaction Flow
The game relies heavily on dynamic possession (e.g., leaving a humanoid body to fly a free-camera, or boarding an airship).
- **IPossessable:** Implemented by entities that can be owned by a player (e.g., Humanoid, Airship).
- **IPossessionApi:** Authoritatively assigns ownership on the server and broadcasts `OnPossessionReceived` events.
- **InteractivityUseCase:** A global `ITickable` that listens for the Interact input. It uses an `IInteractorRegistry` to find what the player is looking at, and routes the request to the `InteractionOrchestrator` (e.g., to board a vehicle).

## Extensibility Points

When adding a new feature (like a new vehicle or weapon):
1. Create pure C# domain logic (e.g., `GunShootingProcessor`).
2. Create an `IUseCase` to bind input and trigger the logic.
3. If it needs networking, create a `WeaponNetworkMediator` (`NetworkBehaviour`) and attach it to the prefab.
4. Bind everything in `ProjectLifetimeScope`.
