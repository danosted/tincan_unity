#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Server: puts networked gas pockets into and out of the world.</summary>
    public interface IGasPocketSpawner
    {
        IGasPocket? Spawn(Vector3 position);
        void Despawn(IGasPocket pocket);
    }
}
