Here is a visualization of the different network states and roles you can encounter within a Unity Netcode for GameObjects (NGO) `NetworkBehaviour`.

The state of a `NetworkBehaviour` depends on two main factors: the **Network Topology Role** (what the current machine is running as) and the **Object Ownership** (who controls the specific object).

### 1. Network Instance Roles
This table shows the global state of the network instance running the game.

| Network Role | `IsServer` | `IsClient` | `IsHost` | Description |
| :--- | :---: | :---: | :---: | :--- |
| **Dedicated Server (Headless)** | **True** | False | False | Runs game logic and state authority. No local player, no rendering. |
| **Host (Server + Client)** | **True** | **True** | **True** | Acts as the authoritative server while also rendering and playing as a local client. |
| **Client (Connected)** | False | **True** | False | A standard player connected to a Server or Host. |
| **Offline** | False | False | False | The `NetworkManager` is stopped or hasn't started yet. |

---

### 2. Object-Specific States
Even if a machine is a Client or a Server, the state of individual `NetworkBehaviour` scripts changes based on who owns the `NetworkObject` they are attached to.

| Scenario | Role Checking | `IsOwner` | `IsLocalPlayer` | `IsSpawned` |
| :--- | :--- | :---: | :---: | :---: |
| **Local Player Avatar** | Client or Host | **True** | **True** | True |
| **Another Player's Avatar** | Client or Host | False | False | True |
| **Player's Item/Weapon** | Client or Host | **True** | False | True |
| **NPC / Enemy / Prop** | Dedicated Server | **True** | False | True |
| **NPC / Enemy / Prop** | Connected Client | False | False | True |
| **Prefab in Project (Not Instantiated)** | Any | False | False | False |

---

### Key Property Definitions
When writing your logic inside a `NetworkBehaviour`, you combine these properties to execute code in the right place:

*   **`IsServer`**: True if this code is running on the machine with authority (Dedicated Server or Host). Use this to restrict logic that only the server should run (e.g., validating hits, spawning objects).
*   **`IsClient`**: True if this code is running on a machine that has a player connection (Host or Connected Client). Use this for client-side predictions, UI updates, and visual effects.
*   **`IsHost`**: True only if running as both Server and Client.
*   **`IsOwner`**: True if the local connection has the authority to send ServerRPCs for this object and is recognized as the owner by the server.
*   **`IsLocalPlayer`**: True only if this specific `NetworkBehaviour` is attached to the main `PlayerObject` assigned to the local connection.
*   **`IsSpawned`**: True if the object has been successfully replicated across the network. If false, RPCs and NetworkVariables cannot be used yet.
