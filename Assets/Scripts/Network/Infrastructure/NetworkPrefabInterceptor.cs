using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Features.Interaction;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Intercepts NGO instantiation to ensure VContainer injection
    /// happens on all clients and the host. Also handles capability registration.
    /// </summary>
    public class NetworkPrefabInterceptor : INetworkPrefabInstanceHandler
    {
        private readonly IObjectResolver _resolver;
        private readonly IActorOrchestrator _orchestrator;
        private readonly GameObject _prefab;

        public NetworkPrefabInterceptor(IObjectResolver resolver, IActorOrchestrator orchestrator, GameObject prefab)
        {
            _resolver = resolver;
            _orchestrator = orchestrator;
            _prefab = prefab;
        }

        public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
        {
            // This method is called by NGO on clients (and host proxies) when a player spawns.
            var instance = Object.Instantiate(_prefab, position, rotation);

            // Ensure the entire hierarchy is injected before NGO's internal callbacks
            _resolver.InjectGameObject(instance);

            // Orchestration: Register actor and its capabilities
            _orchestrator.RegisterHierarchy(instance);

            return instance.GetComponent<NetworkObject>();
        }

        public void Destroy(NetworkObject networkObject)
        {
            _orchestrator.UnregisterHierarchy(networkObject.gameObject);
            Object.Destroy(networkObject.gameObject);
        }
    }
}
