#nullable enable
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// Server-side spawner for island trees, the shape of the placed campfires' (<c>CampSpawningService</c>): world space,
    /// never parented. The spawn carries the tree's position, turn and scale to every peer.
    /// </summary>
    public sealed class NetworkIslandTreeSpawner : IIslandTreeSpawner
    {
        private readonly NetworkManager _networkManager;
        private readonly IObjectResolver _container;
        private readonly IReadOnlyList<GameObject> _prefabs;

        public NetworkIslandTreeSpawner(NetworkManager networkManager, IObjectResolver container, IReadOnlyList<GameObject> prefabs)
        {
            _networkManager = networkManager;
            _container = container;
            _prefabs = prefabs;
        }

        public ulong? Spawn(Vector3 position, Quaternion rotation, float scale, int variant)
        {
            if (!_networkManager.IsServer || variant < 0 || variant >= _prefabs.Count || _prefabs[variant] == null) return null;

            var prefab = _prefabs[variant];
            var instance = Object.Instantiate(prefab, position, rotation);
            instance.transform.localScale = prefab.transform.localScale * scale;
            _container.InjectGameObject(instance);

            var netObj = instance.GetComponent<NetworkObject>();
            if (netObj == null)
            {
                Debug.LogWarning($"[IslandDetails] {prefab.name} needs a NetworkObject; destroying instance.");
                Object.Destroy(instance);
                return null;
            }

            netObj.Spawn();
            return netObj.NetworkObjectId;
        }

        public void Despawn(ulong treeId)
        {
            if (!_networkManager.IsServer) return;
            if (_networkManager.SpawnManager.SpawnedObjects.TryGetValue(treeId, out var netObj) && netObj != null && netObj.IsSpawned)
            {
                netObj.Despawn(true);
            }
        }
    }
}
