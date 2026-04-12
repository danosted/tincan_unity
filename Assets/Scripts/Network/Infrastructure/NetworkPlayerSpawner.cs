using UnityEngine;
using Unity.Netcode;
using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain.Networking;
using TinCan.Features.Possession;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Implements player spawning by bridging VContainer and Netcode.
    /// This ensures every spawned player is fully injected before it is networked.
    /// </summary>
    public class NetworkPlayerSpawner : INetworkPlayerSpawner
    {
        private readonly IObjectResolver _container;

        public NetworkPlayerSpawner(IObjectResolver container)
        {
            _container = container;
        }

        public void SpawnPlayer(ulong clientId, GameObject prefab)
        {
            // 1. Create the instance locally (Server-side)
            var instance = Object.Instantiate(prefab);

            // 2. Inject dependencies immediately
            // Note: The Interceptor handles proxies on clients, but the Spawner handles the initial Server instance
            _container.InjectGameObject(instance);

            // 3. Register as a player object in Netcode
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);

                // 4. Initialize possession state on the server
                var possessable = instance.GetComponent<IPossessable>();
                if (possessable != null)
                {
                    possessable.OnPossessed(clientId);
                }

                Debug.Log($"[NetworkPlayerSpawner] Successfully spawned and possessed player for client {clientId}");
            }
            else
            {
                Debug.LogError($"[NetworkPlayerSpawner] Prefab {prefab.name} is missing a NetworkObject component!");
            }
        }
    }
}
