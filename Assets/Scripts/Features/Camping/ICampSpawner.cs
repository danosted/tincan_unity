#nullable enable
using UnityEngine;

namespace TinCan.Features.Camping
{
    /// <summary>Server: puts a placed campfire or tent into the world. Identified by its network object id.</summary>
    public interface ICampSpawner
    {
        /// <summary>Spawns the prefab for <paramref name="kind"/>; null on a client or when the prefab is missing.</summary>
        ulong? Spawn(CampPlaceable kind, Vector3 position, Quaternion rotation);
    }
}
