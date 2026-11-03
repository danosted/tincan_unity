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
    public class NGONetworkService : INetworkService, INetworkConnections, IInitializable, IDisposable
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

        public Func<ConnectionRequest, ConnectionDecision> Approval { get; set; }
        public event Action<ulong> ClientConnected;
        public event Action<ulong, string> ClientDisconnected;

        public void SetConnectionPayload(byte[] payload)
        {
            if (_manager != null) _manager.NetworkConfig.ConnectionData = payload ?? Array.Empty<byte>();
        }

        // Approval is always on, so every peer's NetworkConfig agrees; with no Approval set everyone is let in. The player
        // object is never NGO's: OnClientConnected spawns it through the spawner.
        private void ApproveConnection(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            var decision = Approval?.Invoke(new ConnectionRequest(request.ClientNetworkId, request.ClientNetworkId == NetworkManager.ServerClientId, request.Payload))
                ?? ConnectionDecision.Approve();
            response.Approved = decision.Approved;
            response.Reason = decision.Reason;
            response.CreatePlayerObject = false;
            response.Pending = false;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            // On a client, the server's refusal (or kick) reason; the server has none to read for its clients.
            string reason = _manager != null && !_manager.IsServer ? ServerReason(_manager.DisconnectReason) : null;
            ClientDisconnected?.Invoke(clientId, reason);
        }

        private const string GeneratedReasonPrefix = "[Disconnect Event]";

        /// <summary>
        /// The reason the server gave, or null. NGO's <c>DisconnectReason</c> is the server's text when it sent one, else
        /// a line NGO writes itself ("[Disconnect Event][Client-1]... The transport was shutdown."), which is no reason
        /// to show a player (NGO 2.11, <c>NetworkConnectionManager.GenerateDisconnectInformation</c>).
        /// </summary>
        public static string ServerReason(string disconnectReason) =>
            string.IsNullOrWhiteSpace(disconnectReason) || disconnectReason.StartsWith(GeneratedReasonPrefix, StringComparison.Ordinal)
                ? null
                : disconnectReason;

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
                _manager.OnClientDisconnectCallback += OnClientDisconnected;
                _manager.NetworkConfig.ConnectionApproval = true;
                _manager.ConnectionApprovalCallback = ApproveConnection;
            }
        }

        public void Dispose()
        {
            if (_manager != null)
            {
                _manager.OnClientConnectedCallback -= OnClientConnected;
                _manager.OnClientDisconnectCallback -= OnClientDisconnected;
                if (_manager.ConnectionApprovalCallback == ApproveConnection) _manager.ConnectionApprovalCallback = null;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[NGONetworkService] Client connected with ID: {clientId}");
            ClientConnected?.Invoke(clientId);
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
