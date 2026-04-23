# Network Initialization Flow

This diagram illustrates how VContainer dependency injection integrates with Netcode for GameObjects (NGO) during the initialization of networked components across different network topologies (Host, Dedicated Server, and Client).

```mermaid
sequenceDiagram
    participant LS as ProjectLifetimeScope
    participant VC as VContainer
    participant NM as NetworkManager (NGO)
    participant NS as NGONetworkService
    participant NPS as NetworkPlayerSpawner
    participant NPI as NetworkPrefabInterceptor
    participant AO as ActorOrchestrator
    participant NO as NetworkObject (Prefab)
    participant NMd as NetworkMediator

    Note over LS, NM: Phase 1: Setup & Registration (All Nodes)
    LS->>VC: Configure(builder)
    VC->>NS: Instantiate Service
    VC->>NPS: Instantiate Spawner

    LS->>LS: RegisterBuildCallback()
    LS->>LS: AddNetworkedPrefab(PlayerPrefab)
    LS->>NPI: Create Interceptor for Prefab
    LS->>NM: PrefabHandler.AddHandler(PlayerPrefab, NPI)

    LS->>LS: AddNetworkedPrefab(ServerSingletons)
    LS->>NM: Subscribe to OnServerStarted

    NS->>NM: Subscribe to OnClientConnectedCallback

    alt Headless Server / Host Starting
        Note over LS, NMd: Phase 2: Server Initialization (Host or Dedicated Server)
        NM->>NM: StartServer() / StartHost()

        Note right of NM: Fires OnServerStarted
        NM-->>LS: Trigger OnServerStarted callback
        LS->>NO: Instantiate(ServerSingleton)
        LS->>VC: InjectGameObject(ServerSingleton)
        LS->>NO: Spawn()

        Note right of NM: Client Connects (Including Host's local client)
        NM-->>NS: OnClientConnectedCallback(clientId)
        NS->>NPS: SpawnPlayer(clientId, prefab, isServer)

        NPS->>NO: Instantiate(PlayerPrefab)
        NPS->>VC: InjectGameObject(PlayerPrefab instance)
        NPS->>NO: SpawnAsPlayerObject(clientId)
        NPS->>NPS: NotifyPlayerSpawned()

        NO->>NMd: OnNetworkSpawn()
        NMd->>AO: RegisterHierarchy(gameObject)

    else Client Connecting
        Note over LS, NMd: Phase 3: Client Initialization (Connecting to Server)
        NM->>NM: StartClient()

        Note right of NM: Server calls SpawnAsPlayerObject()
        NM-->>NPI: Receive Spawn RPC from Server
        NPI->>NPI: Instantiate(ownerClientId, pos, rot)

        Note over NPI, VC: VContainer hooks in BEFORE NGO logic
        NPI->>VC: InjectGameObject(instance)
        NPI->>LS: configureInit callback (Check if Local Client)

        opt If Local Client
            LS->>NPS: NotifyPlayerSpawned(instance, true)
        end

        NPI-->>NM: return NetworkObject component

        Note over NM, NMd: NGO completes spawn setup
        NM->>NMd: OnNetworkSpawn()
        NMd->>AO: RegisterHierarchy(gameObject)
    end
```
