#nullable enable
using UnityEngine;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// Server: puts networked island details (trees, crystal veins) into the world and takes them away. Each is identified
    /// by its network object id.
    /// </summary>
    public interface IIslandTreeSpawner
    {
        /// <summary>Spawns prefab <paramref name="variant"/> of that kind, scaled; null on a client or when there is no such prefab.</summary>
        ulong? Spawn(IslandDetailKind kind, Vector3 position, Quaternion rotation, float scale, int variant);

        void Despawn(ulong treeId);
    }
}
