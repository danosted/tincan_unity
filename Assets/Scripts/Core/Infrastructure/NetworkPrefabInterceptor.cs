#nullable enable
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using System;

namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Intercepts NGO instantiation to ensure VContainer injection
    /// happens on all clients and the host. Also handles capability registration.
    /// </summary>
    public class NetworkPrefabInterceptor : INetworkPrefabInstanceHandler
    {
        private readonly IObjectResolver _container;
        private readonly Action<GameObject, ulong>? _configureInit;
        private readonly Action<GameObject>? _configureDestroy;
        private readonly GameObject _prefab;

        public NetworkPrefabInterceptor(
            IObjectResolver container,
            GameObject prefab,
            Action<GameObject, ulong>? configureInit = null,
            Action<GameObject>? configureDestroy = null)
        {
            _container = container;
            _configureInit = configureInit;
            _configureDestroy = configureDestroy;
            _prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            // This method is called by NGO on clients (and host proxies) when a player spawns.
            var instance = UnityEngine.Object.Instantiate(_prefab, position, rotation);

            // Ensure the entire hierarchy is injected before NGO's internal callbacks
            _container.InjectGameObject(instance);

            // Orchestration: Register actor and its capabilities
            _configureInit?.Invoke(instance, ownerClientId);

            return instance.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            _configureDestroy?.Invoke(networkObject.gameObject);
            UnityEngine.Object.Destroy(networkObject.gameObject);
        }
    }
}
