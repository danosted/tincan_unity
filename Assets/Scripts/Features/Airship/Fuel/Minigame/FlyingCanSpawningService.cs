#nullable enable
using TinCan.Core.Domain;
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
        private readonly ITimeService _timeService;

        public FlyingCanSpawningService(NetworkManager networkManager, IObjectResolver container, FlyingCanConfig config, ITimeService timeService)
        {
            _networkManager = networkManager;
            _container = container;
            _config = config;
            _timeService = timeService;
        }

        public IFlyingCanView? Spawn(Vector3 position, Vector3 velocity)
        {
            if (!_networkManager.IsServer || _config == null || _config.CanPrefab == null) return null;

            var instance = Object.Instantiate(_config.CanPrefab, position, Quaternion.LookRotation(velocity.sqrMagnitude > 0f ? velocity : Vector3.forward));
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogWarning("[FlyingCanSpawningService] CanPrefab has no NetworkObject; destroying instance.");
                Object.Destroy(instance);
                return null;
            }

            netObj.Spawn();

            var can = instance.GetComponent<IFlyingCanView>();
            if (can != null)
            {
                can.Velocity = velocity;
                can.SpawnTime = _timeService.Time; // every can gets a lifetime, including ones spawned by tools
            }
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
