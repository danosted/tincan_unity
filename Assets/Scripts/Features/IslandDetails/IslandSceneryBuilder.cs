#nullable enable
using System.Collections.Generic;
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// Infrastructure: an island's boulders and bushes as instances of the config's prefabs under an <c>IslandScenery</c>
    /// root in the active scene. Islands stand still, so the scenery is placed once in world space. Removed scenery goes
    /// back to a pool per prefab, so streaming does not churn objects.
    /// </summary>
    public sealed class IslandSceneryBuilder : IIslandSceneryBuilder
    {
        private readonly IslandDetailsConfig _config;
        private readonly Dictionary<GameObject, Stack<GameObject>> _pools = new();
        private readonly HashSet<Built> _standing = new();
        private Transform? _root;

        public IslandSceneryBuilder(IslandDetailsConfig config) => _config = config;

        public object Build(in SkyIslandSpec island, IReadOnlyList<IslandDetail> details)
        {
            var built = new Built();
            foreach (var detail in details)
            {
                var prefab = PrefabFor(detail);
                if (prefab == null) continue;

                var instance = Take(prefab);
                instance.transform.SetPositionAndRotation(island.Top + detail.Position, detail.Rotation);
                instance.transform.localScale = prefab.transform.localScale * detail.Scale;
                instance.SetActive(true);
                built.Instances.Add((prefab, instance));
            }

            _standing.Add(built);
            return built;
        }

        public void Remove(object handle)
        {
            if (handle is not Built built || !_standing.Remove(built)) return;
            foreach (var (prefab, instance) in built.Instances)
            {
                if (instance == null) continue;
                instance.SetActive(false);
                Pool(prefab).Push(instance);
            }
        }

        public void Clear()
        {
            _standing.Clear();
            _pools.Clear();
            if (_root != null) Discard(_root.gameObject);
            _root = null;
        }

        /// <summary>The prefab for a detail, or null for trees (networked, not scenery) and missing variants.</summary>
        private GameObject? PrefabFor(in IslandDetail detail)
        {
            var list = detail.Kind switch
            {
                IslandDetailKind.Boulder => _config.BoulderPrefabs,
                IslandDetailKind.Bush => _config.BushPrefabs,
                _ => null
            };
            return list != null && detail.Variant < list.Count ? list[detail.Variant] : null;
        }

        private GameObject Take(GameObject prefab)
        {
            var pool = Pool(prefab);
            while (pool.Count > 0)
            {
                var pooled = pool.Pop();
                if (pooled != null) return pooled;
            }

            var instance = Object.Instantiate(prefab, Root());
            instance.name = prefab.name;
            return instance;
        }

        private Stack<GameObject> Pool(GameObject prefab)
        {
            if (!_pools.TryGetValue(prefab, out var pool)) _pools[prefab] = pool = new Stack<GameObject>();
            return pool;
        }

        private Transform Root()
        {
            if (_root != null) return _root;
            _root = new GameObject("IslandScenery").transform;
            return _root;
        }

        /// <summary>Destroy, or DestroyImmediate outside play mode (edit-mode tests).</summary>
        private static void Discard(Object target)
        {
            if (Application.isPlaying) Object.Destroy(target);
            else Object.DestroyImmediate(target);
        }

        private sealed class Built
        {
            public readonly List<(GameObject Prefab, GameObject Instance)> Instances = new();
        }
    }
}
