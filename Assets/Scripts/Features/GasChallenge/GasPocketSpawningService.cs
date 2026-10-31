#nullable enable
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Server-side spawner for gas pockets; same shape as SkyHazardSpawningService (world space, never parented).</summary>
    public sealed class GasPocketSpawningService : IGasPocketSpawner
    {
        private readonly NetworkManager _networkManager;
        private readonly IObjectResolver _container;
        private readonly GasChallengeConfig _config;

        public GasPocketSpawningService(NetworkManager networkManager, IObjectResolver container, GasChallengeConfig config)
        {
            _networkManager = networkManager;
            _container = container;
            _config = config;
        }

        public IGasPocket? Spawn(Vector3 position)
        {
            if (!_networkManager.IsServer || _config.PocketPrefab == null) return null;

            var instance = Object.Instantiate(_config.PocketPrefab, position, Quaternion.identity);
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            var pocket = instance.GetComponent<IGasPocket>();
            if (netObj == null || pocket == null)
            {
                Debug.LogWarning("[GasPocketSpawningService] Prefab needs a NetworkObject and a GasPocketNetworkMediator; destroying instance.");
                Object.Destroy(instance);
                return null;
            }

            netObj.Spawn();
            return pocket;
        }

        public void Despawn(IGasPocket pocket)
        {
            if (!_networkManager.IsServer || pocket is not Component component || component == null) return;

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
