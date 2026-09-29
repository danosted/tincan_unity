#nullable enable
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.SkyHazards
{
    /// <summary>Server: puts hazards into and out of the world. The entry point for events that spawn targets later.</summary>
    public interface ISkyHazardSpawner
    {
        ISkyHazard? Spawn(Vector3 position);
        void Despawn(ISkyHazard hazard);
    }

    /// <summary>Server-side spawner for sky hazards; same shape as FlyingCanSpawningService (world space, never parented).</summary>
    public class SkyHazardSpawningService : ISkyHazardSpawner
    {
        private readonly NetworkManager _networkManager;
        private readonly IObjectResolver _container;
        private readonly SkyHazardConfig _config;

        public SkyHazardSpawningService(NetworkManager networkManager, IObjectResolver container, SkyHazardConfig config)
        {
            _networkManager = networkManager;
            _container = container;
            _config = config;
        }

        public ISkyHazard? Spawn(Vector3 position)
        {
            if (!_networkManager.IsServer || _config.Prefab == null) return null;

            var instance = Object.Instantiate(_config.Prefab, position, Quaternion.identity);
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            var hazard = instance.GetComponent<ISkyHazard>();
            if (netObj == null || hazard == null)
            {
                Debug.LogWarning("[SkyHazardSpawningService] Prefab needs a NetworkObject and an ISkyHazard; destroying instance.");
                Object.Destroy(instance);
                return null;
            }

            netObj.Spawn();
            return hazard;
        }

        public void Despawn(ISkyHazard hazard)
        {
            if (!_networkManager.IsServer || hazard is not Component component || component == null) return;

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
