#nullable enable
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.Camping
{
    /// <summary>Server-side spawner for placed campfires and tents: world space, never parented.</summary>
    public sealed class CampSpawningService : ICampSpawner
    {
        private readonly NetworkManager _networkManager;
        private readonly IObjectResolver _container;
        private readonly CampingConfig _config;

        public CampSpawningService(NetworkManager networkManager, IObjectResolver container, CampingConfig config)
        {
            _networkManager = networkManager;
            _container = container;
            _config = config;
        }

        public ulong? Spawn(CampPlaceable kind, Vector3 position, Quaternion rotation)
        {
            var prefab = kind == CampPlaceable.Tent ? _config.TentPrefab : _config.CampfirePrefab;
            if (!_networkManager.IsServer || prefab == null) return null;

            var instance = Object.Instantiate(prefab, position, rotation);
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogWarning($"[Camping] {prefab.name} needs a NetworkObject; destroying instance.");
                Object.Destroy(instance);
                return null;
            }

            netObj.Spawn();
            return netObj.NetworkObjectId;
        }
    }
}
