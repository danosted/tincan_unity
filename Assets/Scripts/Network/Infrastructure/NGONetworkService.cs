using Unity.Netcode;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using System;

namespace TinCan.Network.Infrastructure
{
    public class NGONetworkService : INetworkService, IInitializable, IDisposable
    {
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
                Debug.Log($"[NGONetworkService] Client {clientId} connected. Spawning player...");
                _spawner.SpawnPlayer(clientId, _playerPrefab, IsClient && clientId == LocalClientId);
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

        public void StartHost() => _manager.StartHost();
        public void StartServer() => _manager.StartServer();
        public void StartClient() => _manager.StartClient();
        public void Shutdown() => _manager.Shutdown();
    }
}
