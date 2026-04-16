using UnityEngine;
using Unity.Netcode;
using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain.Networking;
using TinCan.Core.Domain;
using TinCan.Features.Interaction;
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
        private readonly PossessionUseCase _possessionUsecase;

        public NetworkPlayerSpawner(
            IObjectResolver container,
            PossessionUseCase possessionUsecase)
        {
            _container = container;
            _possessionUsecase = possessionUsecase;
        }

        public void SpawnPlayer(ulong clientId, GameObject prefab, bool isLocalPlayer = false)
        {
            // 1. Create the instance locally (Server-side)
            var instance = Object.Instantiate(prefab);

            // 2. Inject dependencies immediately
            // Note: The Interceptor handles proxies on clients, but the Spawner handles the initial Server instance
            _container.InjectGameObject(instance);

            // 3. Orchestration: Initialize player actor in the PossessionUseCase
            if (isLocalPlayer)
            {
                _possessionUsecase.InitializePlayerActor(instance);
            }

            // 4. Register as a player object in Netcode
            var networkObject = instance.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                networkObject.SpawnAsPlayerObject(clientId);
                Debug.Log($"[NetworkPlayerSpawner] Successfully spawned player for client {clientId}");
            }
            else
            {
                Debug.LogError($"[NetworkPlayerSpawner] Prefab {prefab.name} is missing a NetworkObject component!");
            }
        }
    }
}
