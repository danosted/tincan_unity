#nullable enable
using UnityEngine;

namespace TinCan.Features.IslandDetails
{
    /// <summary>Server: puts networked trees into the world and takes them away. A tree is identified by its network object id.</summary>
    public interface IIslandTreeSpawner
    {
        /// <summary>Spawns tree prefab <paramref name="variant"/>, scaled; null on a client or when there is no such prefab.</summary>
        ulong? Spawn(Vector3 position, Quaternion rotation, float scale, int variant);

        void Despawn(ulong treeId);
    }
}
