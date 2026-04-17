#nullable enable
using UnityEngine;
using Unity.Netcode;
using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain.Networking;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Implements player spawning by bridging VContainer and Netcode.
    /// This ensures every spawned player is fully injected before it is networked.
    /// </summary>
    public class NetworkPlayerSpawner : INetworkPlayerSpawner
    {
        public event System.Action<GameObject, ulong, bool>? OnPlayerSpawned;
        private readonly IObjectResolver _container;

        public NetworkPlayerSpawner(IObjectResolver container)
        {
            _container = container;
        }

        public void SpawnPlayer(ulong clientId, GameObject prefab, bool isServer, bool isLocalPlayer = false)
        {
            // 1. Create the instance locally (Server-side)
            var instance = Object.Instantiate(prefab);
            instance.name = $"{prefab.name}_Client{clientId}";

            // 2. Inject dependencies immediately
            _container.InjectGameObject(instance);

            // 4. Register as a player object in Netcode
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Debug.LogError($"[NetworkPlayerSpawner] Prefab {prefab.name} is missing a NetworkObject component!");
                return;
            }

            if (isServer)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"[NetworkPlayerSpawner] SERVER Successfully spawned player for client {clientId}");
            }

            // 3. Notify listeners (breaks circular dependency with PossessionUseCase)
            NotifyPlayerSpawned(instance, clientId, isLocalPlayer);
        }

        public void NotifyPlayerSpawned(GameObject instance, ulong clientId, bool isLocalPlayer)
        {
            OnPlayerSpawned?.Invoke(instance, clientId, isLocalPlayer);
        }
    }
}
