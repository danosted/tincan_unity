#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>
    /// An island's collision as convex pieces: each piece is a point cloud whose convex hull is one wedge of one slab
    /// between two rings (or a ring and the top's centre or the apex). Convex pieces can be tested against any collider the
    /// ship has, non-convex hulls included (<c>Physics.ComputePenetration</c> needs one side convex). Filled by
    /// <see cref="SkyIslandMeshProcessor.BuildPieces"/> and reused between builds.
    /// </summary>
    public sealed class SkyIslandCollisionPieces
    {
        /// <summary>Every piece's points, one after the other.</summary>
        public readonly List<Vector3> Points = new();

        /// <summary>Where each piece starts in <see cref="Points"/>; it runs to the next start, or the end.</summary>
        public readonly List<int> Starts = new();

        public int Count => Starts.Count;

        public int Length(int piece) => (piece + 1 < Starts.Count ? Starts[piece + 1] : Points.Count) - Starts[piece];

        public void Clear()
        {
            Points.Clear();
            Starts.Clear();
        }

        public void BeginPiece() => Starts.Add(Points.Count);
    }
}
