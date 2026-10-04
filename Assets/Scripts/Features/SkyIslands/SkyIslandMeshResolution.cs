#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>How finely an island's mesh is cut: points around each ring, rings on the top, rings down the underside.</summary>
    [Serializable]
    public struct SkyIslandMeshResolution
    {
        [Min(6)] public int Segments;
        [Min(1)] public int TopRings;
        [Min(1)] public int UnderRings;

        public SkyIslandMeshResolution(int segments, int topRings, int underRings)
        {
            Segments = segments;
            TopRings = topRings;
            UnderRings = underRings;
        }

        /// <summary>Never coarser than a closed shape can be built from.</summary>
        public SkyIslandMeshResolution Clamped =>
            new(Math.Max(6, Segments), Math.Max(1, TopRings), Math.Max(1, UnderRings));

        /// <summary>Centre and apex, plus the top rings, the rim and the underside rings.</summary>
        public int VertexCount => 2 + Clamped.Segments * (Clamped.TopRings + 1 + Clamped.UnderRings);
    }
}
