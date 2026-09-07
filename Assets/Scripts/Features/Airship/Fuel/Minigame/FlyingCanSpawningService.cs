#nullable enable
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Airship.Fuel.Minigame
{
    /// <summary>
    /// Server-side spawner for flying cans. Same shape as ModuleSpawningService minus the parenting: cans live in
    /// world space and are never attached to the ship.
    /// </summary>
    public class FlyingCanSpawningService : IFlyingCanSpawner
    {
        private readonly NetworkManager _networkManager;
        private readonly IObjectResolver _container;
        private readonly FlyingCanConfig _config;

        public FlyingCanSpawningService(NetworkManager networkManager, IObjectResolver container, FlyingCanConfig config)
        {
            _networkManager = networkManager;
            _container = container;
            _config = config;
        }

        public IFlyingCanView? Spawn(Vector3 position)
        {
            if (!_networkManager.IsServer || _config == null || _config.CanPrefab == null) return null;

            var instance = Object.Instantiate(_config.CanPrefab, position, Quaternion.identity);
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            var can = instance.GetComponent<IFlyingCanView>();
            if (netObj == null || can == null)
            {
                Debug.LogWarning("[FlyingCanSpawningService] CanPrefab needs a NetworkObject and IFlyingCanView; destroying instance.");
                Object.Destroy(instance);
                return null;
            }

            netObj.Spawn();

            return can;
        }

        public void Despawn(IFlyingCanView can)
        {
            if (!_networkManager.IsServer || can is not Component component || component == null) return;

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
