using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Intercepts NGO instantiation to ensure VContainer injection
    /// happens on all clients and the host.
    /// </summary>
    public class NetworkPrefabInterceptor : INetworkPrefabInstanceHandler
    {
        private readonly IObjectResolver _resolver;
        private readonly GameObject _prefab;

        public NetworkPrefabInterceptor(IObjectResolver resolver, GameObject prefab)
        {
            _resolver = resolver;
            _prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            // This method is called by NGO on clients (and host proxies) when a player spawns.
            var instance = Object.Instantiate(_prefab, position, rotation);

            // Ensure the entire hierarchy is injected before NGO's internal callbacks
            _resolver.InjectGameObject(instance);

            return instance.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            Object.Destroy(networkObject.gameObject);
        }
    }
}
