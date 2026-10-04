#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// How big a design may be. Checked when a design is read, before anything is built, so a broken or hostile file
    /// cannot stall a peer; the validator and the edit processor check the same numbers.
    /// </summary>
    [Serializable]
    public struct ShipDesignLimits
    {
        [Tooltip("Most parts in one design.")]
        [Min(1)] public int MaxParts;
        [Tooltip("No part cell may be further than this many cells from the origin on any axis.")]
        [Min(1)] public int MaxExtent;
        [Tooltip("Longest design file, in characters.")]
        [Min(1024)] public int MaxTextLength;
        [Tooltip("Longest design name, in characters.")]
        [Min(1)] public int MaxNameLength;

        public static ShipDesignLimits Default => new()
        {
            MaxParts = 4096,
            MaxExtent = 128,
            MaxTextLength = 2_000_000,
            MaxNameLength = 64,
        };

        public bool Contains(ShipGridCell cell) =>
            Math.Abs(cell.X) <= MaxExtent && Math.Abs(cell.Y) <= MaxExtent && Math.Abs(cell.Z) <= MaxExtent;
    }
}
