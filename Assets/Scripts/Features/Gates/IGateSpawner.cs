#nullable enable
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>Server: puts gates into and out of the world.</summary>
    public interface IGateSpawner
    {
        IGate? Spawn(GameObject prefab, Vector3 position);
        void Despawn(IGate gate);
    }
}
