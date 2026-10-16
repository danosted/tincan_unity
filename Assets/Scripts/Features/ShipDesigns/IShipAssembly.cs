#nullable enable
using System;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>What this peer has built for each ship: the design's hash, how many parts and frame meshes stand.</summary>
    public interface IShipAssembly
    {
        bool TryGetBuilt(Guid shipId, out ulong hash, out int builtParts);

        /// <summary>How many generated frame meshes (planks and beams per story) stand for the ship.</summary>
        int BuiltFrameMeshes(Guid shipId);
    }
}
