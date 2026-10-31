#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>A networked gas pocket as every peer sees it (registered as an actor by its entity).</summary>
    public interface IGasPocket : IActor
    {
        Vector3 Center { get; }
        float Radius { get; }

        /// <summary>Replicated: the pocket has gone off (its cloud is gone).</summary>
        bool Detonated { get; }

        /// <summary>The server-side volume the touch rule works on.</summary>
        GasPocketVolume Volume { get; }
    }
}
