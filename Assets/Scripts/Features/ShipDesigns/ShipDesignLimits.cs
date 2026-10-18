#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// How big a design may be. Checked when a design is read, before anything is built, so a broken or hostile file
    /// cannot stall a peer; the validator and the edit processor check the same numbers. The frame shares the extent:
    /// no node or part block further than <see cref="MaxExtent"/> metres from the origin.
    /// </summary>
    [Serializable]
    public struct ShipDesignLimits
    {
        [Tooltip("Most parts in one design.")]
        [Min(1)] public int MaxParts;
        [Tooltip("No part block or frame node may be further than this many metres from the origin on any axis.")]
        [Min(1)] public int MaxExtent;
        [Tooltip("Longest design file, in characters.")]
        [Min(1024)] public int MaxTextLength;
        [Tooltip("Longest design name, in characters.")]
        [Min(1)] public int MaxNameLength;
        [Tooltip("Most frame nodes, edges and faces together in one design.")]
        [Min(1)] public int MaxFrameElements;
        [Tooltip("Most frame stories.")]
        [Min(1)] public int MaxStories;
        [Tooltip("Most corners on one frame face.")]
        [Min(3)] public int MaxFaceNodes;

        public static ShipDesignLimits Default => new()
        {
            MaxParts = 4096,
            MaxExtent = 128,
            MaxTextLength = 2_000_000,
            MaxNameLength = 64,
            MaxFrameElements = 16384,
            MaxStories = 8,
            MaxFaceNodes = 64,
        };

        /// <summary>Whether a frame point (in frame units) is within the extent.</summary>
        public bool Contains(int x, int y, int z)
        {
            long reach = (long)MaxExtent * ShipFrameUnits.PerMetre;
            return Math.Abs((long)x) <= reach && Math.Abs((long)y) <= reach && Math.Abs((long)z) <= reach;
        }
    }
}
