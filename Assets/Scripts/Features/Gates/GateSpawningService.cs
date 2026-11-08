#nullable enable
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Gates
{
    /// <summary>Server-side spawner for gates; same shape as the sky hazards' (world space, never parented).</summary>
    public class GateSpawningService : IGateSpawner
    {
        private readonly NetworkManager _networkManager;
        private readonly IObjectResolver _container;

        public GateSpawningService(NetworkManager networkManager, IObjectResolver container)
        {
            _networkManager = networkManager;
            _container = container;
        }

        public IGate? Spawn(GameObject prefab, Vector3 position)
        {
            if (!_networkManager.IsServer) return null;

            var instance = Object.Instantiate(prefab, position, Quaternion.identity);
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            var gate = instance.GetComponent<IGate>();
            if (netObj == null || gate == null)
            {
                Debug.LogWarning($"[GateSpawningService] {prefab.name} needs a NetworkObject and an IGate; destroying instance.");
                Object.Destroy(instance);
                return null;
            }

            netObj.Spawn();
            return gate;
        }

        public void Despawn(IGate gate)
        {
            if (!_networkManager.IsServer || gate is not Component component || component == null) return;

            var netObj = component.GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
                return;
            }

            Object.Destroy(component.gameObject);
        }
    }
}
