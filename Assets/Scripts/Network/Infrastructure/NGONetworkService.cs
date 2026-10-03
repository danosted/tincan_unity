using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using System;

namespace TinCan.Network.Infrastructure
{
    public class NGONetworkService : INetworkService, IInitializable, IDisposable
    {
        private const int HeadlessClientFrameRate = 60;

        private readonly NetworkManager _manager;
        private readonly INetworkPlayerSpawner _spawner;
        private GameObject _playerPrefab;

        public NGONetworkService(NetworkManager manager, INetworkPlayerSpawner spawner)
        {
            _manager = manager;
            _spawner = spawner;
        }

        public void SetPlayerPrefab(GameObject prefab)
        {
            Debug.Log($"[NGONetworkService] Player prefab set to: {prefab.name}");
            _playerPrefab = prefab;
        }

        public void Initialize()
        {
            Debug.Log("[NGONetworkService] Initializing and subscribing to client connection events.");
            if (_manager != null)
            {
                Debug.Log("[NGONetworkService] Subscribing to OnClientConnectedCallback.");
                _manager.OnClientConnectedCallback += OnClientConnected;
            }
        }

        public void Dispose()
        {
            if (_manager != null)
            {
                _manager.OnClientConnectedCallback -= OnClientConnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[NGONetworkService] Client connected with ID: {clientId}");
            // If we are the server (or host), spawn the player when they connect
            if (IsServer && _playerPrefab != null)
            {
                Debug.Log($"[NGONetworkService] SERVER: Client {clientId} connected. Spawning player...");
                _spawner.SpawnPlayer(clientId, _playerPrefab, IsServer, clientId == LocalClientId);
            }
        }

        public NetworkState State
        {
            get
            {
                if (_manager == null || !_manager.IsListening) return NetworkState.Offline;
                if (_manager.IsHost) return NetworkState.Host;
                if (_manager.IsServer) return NetworkState.Server;
                if (_manager.IsClient) return NetworkState.Client;
                return NetworkState.Connecting;
            }
        }

        public bool IsActive => _manager != null && _manager.IsListening;
        public bool IsServer => _manager != null && _manager.IsServer;
        public bool IsClient => _manager != null && _manager.IsClient;
        public bool IsHost => _manager != null && _manager.IsHost;
        public ulong LocalClientId => _manager != null ? _manager.LocalClientId : 0;

        public void SetConnection(string address, ushort port)
        {
            var transport = _manager != null ? _manager.GetComponent<UnityTransport>() : null;
            if (transport == null)
            {
                Debug.LogWarning("[NGONetworkService] No UnityTransport on the NetworkManager; cannot set connection data.");
                return;
            }
            // A remote join destination must not become the bind address for a later host session.
            transport.SetConnectionData(address, port, transport.ConnectionData.ServerListenAddress ?? string.Empty);
        }

        public void SetListenEndpoint(string listenAddress, ushort port)
        {
            var transport = _manager != null ? _manager.GetComponent<UnityTransport>() : null;
            if (transport == null)
            {
                Debug.LogWarning("[NGONetworkService] No UnityTransport on the NetworkManager; cannot set the listen endpoint.");
                return;
            }
            // Forced: the explicit endpoint wins over NGO's own -port / -ip command-line overrides.
            transport.SetConnectionData(true, transport.ConnectionData.Address, port, listenAddress);
        }

        public void StartHost() => _manager.StartHost();
        public void StartServer()
        {
            // A dedicated server draws nothing and has no vsync to pace it: uncapped, its main loop spins on every core
            // it can get (measured 3.5 cores idle in a container). The cap defaults to the network tick rate;
            // -serverfps <n> overrides it (the transport is read once per frame, so the cap bounds input latency:
            // .docs/plans/performance-budgets.md, Risks).
            Application.targetFrameRate = ServerFrameRate(LaunchArguments.Current, (int)_manager.NetworkConfig.TickRate);
            _manager.StartServer();
        }

        public const string ServerFpsFlag = "-serverfps";

        /// <summary>The dedicated server's frame cap: <c>-serverfps &lt;n&gt;</c> when given and at least the tick rate, else the tick rate.</summary>
        public static int ServerFrameRate(System.Collections.Generic.IReadOnlyList<string> args, int tickRate) =>
            LaunchArguments.TryGetValue(args, ServerFpsFlag, out var value) && int.TryParse(value, out var fps) && fps >= tickRate
                ? fps
                : tickRate;
        public void StartClient()
        {
            // A batch-mode client (a headless bot) has no vsync either: uncapped it spun at ~4,600 fps on ~3 cores
            // (2026-10-02). Cap it at a player's frame rate, so bots cost what a player costs and leave cores for the rest.
            if (Application.isBatchMode) Application.targetFrameRate = HeadlessClientFrameRate;
            _manager.StartClient();
        }
        public void Shutdown() => _manager.Shutdown();
    }
}
