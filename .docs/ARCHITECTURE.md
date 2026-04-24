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
- **Naming Convention:** All components that inherit from `NetworkBehaviour` MUST be suffixed with `NetworkMediator` (e.g., `AbilityNetworkMediator`, not `AbilityController`).
- **NetworkMediators:** MonoBehaviours that act as the authoritative bridge. Logic should be separated into pure C# `UseCase` or `Processor` classes, while the Mediator handles the RPCs and `NetworkVariables`.
- **Prefab Spawning:** Controlled strictly via VContainer's `AddNetworkedPrefab` extensions. See `ProjectLifetimeScope` for examples of how we spawn the player and airship.

### 3. Simulation & Synchronization Paradigms
To maintain a responsive FPS experience, we follow an **Input-Driven Simulation** paradigm:
- **Input-Driven Simulation (Input Sync):** Primary for movement and time-critical actions. Clients capture intent as an `InputState`. Both Client (Prediction) and Server (Authority) execute the same Use Case logic using this input stream.
- **State-Driven Synchronization (State Sync):** The server is the source of truth for high-level state changes (Tags, Attributes, Inventory). Mediators sync these back to clients via `NetworkVariable` or `ClientRpc` for visual confirmation.
- **Avoid Side-Channels:** Do not use independent `ServerRpc` calls for actions that are part of the core simulation loop (like ability triggers or jumping). These should be bits in the `InputState` to ensure they are processed at the correct simulation tick.

### 4. Possession & Interaction Flow
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
