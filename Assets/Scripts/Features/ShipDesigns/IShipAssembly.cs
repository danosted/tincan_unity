#nullable enable
using System;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>What this peer has built for each ship: the design's hash and how many parts stand.</summary>
    public interface IShipAssembly
    {
        bool TryGetBuilt(Guid shipId, out ulong hash, out int builtParts);
    }
}
