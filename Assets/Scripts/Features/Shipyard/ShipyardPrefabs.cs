#nullable enable
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// Copies of part prefabs for the shipyard, which runs offline: every script is removed before the copy wakes up
    /// (networked parts like the helm carry NetworkObjects and mediators that need a session), so only meshes, colliders
    /// and other built-in components remain.
    /// </summary>
    public static class ShipyardPrefabs
    {
        public static GameObject InstantiateStripped(GameObject prefab, Transform parent, bool keepColliders)
        {
            var holder = new GameObject("ShipyardHolder");
            holder.SetActive(false);
            try
            {
                var copy = Object.Instantiate(prefab, holder.transform, false);
                Strip(copy, keepColliders);
                copy.transform.SetParent(parent, false);
                return copy;
            }
            finally
            {
                Object.Destroy(holder);
            }
        }

        private static void Strip(GameObject copy, bool keepColliders)
        {
            // Later components usually depend on earlier ones (RequireComponent), so remove from the back.
            var scripts = copy.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = scripts.Length - 1; i >= 0; i--) Object.DestroyImmediate(scripts[i]);
            foreach (var body in copy.GetComponentsInChildren<Rigidbody>(true)) Object.DestroyImmediate(body);
            if (keepColliders) return;
            foreach (var collider in copy.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(collider);
        }
    }
}
