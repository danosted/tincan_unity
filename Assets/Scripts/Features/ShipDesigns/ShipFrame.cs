#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// A ship's frame: stories stacked from <see cref="Base"/> up, nodes on them, edges (beams) between nodes and faces
    /// (plank floors or open holes) bounded by edges. Plain data in frame units; immutable, kept in id order so it always
    /// encodes to the same bytes. The hull is generated from it (<see cref="ShipFrameMeshProcessor"/>); catalog parts sit
    /// on it. Plan: .docs/plans/ship-frames.md.
    /// </summary>
    public sealed class ShipFrame
    {
        public ShipFrame(int @base, IEnumerable<int> storyHeights, IEnumerable<ShipFrameNode> nodes, IEnumerable<ShipFrameEdge> edges,
            IEnumerable<ShipFrameFace> faces)
        {
            Base = @base;
            StoryHeights = storyHeights.ToList();
            Nodes = nodes.OrderBy(n => n.Id).ToList();
            Edges = edges.OrderBy(e => e.Id).ToList();
            Faces = faces.OrderBy(f => f.Id).ToList();
        }

        public static ShipFrame Empty { get; } = new(0, Array.Empty<int>(), Array.Empty<ShipFrameNode>(), Array.Empty<ShipFrameEdge>(),
            Array.Empty<ShipFrameFace>());

        /// <summary>The height of story 0's floor plane in ship space, in frame units.</summary>
        public int Base { get; }

        /// <summary>Each story's height in frame units, from story 0 up: story s+1's floor is story s's floor plus its height.</summary>
        public IReadOnlyList<int> StoryHeights { get; }

        public IReadOnlyList<ShipFrameNode> Nodes { get; }
        public IReadOnlyList<ShipFrameEdge> Edges { get; }
        public IReadOnlyList<ShipFrameFace> Faces { get; }

        public bool IsEmpty => StoryHeights.Count == 0 && Nodes.Count == 0 && Edges.Count == 0 && Faces.Count == 0;

        public int ElementCount => Nodes.Count + Edges.Count + Faces.Count;

        /// <summary>Every id the frame uses (nodes, edges, faces), in no particular order.</summary>
        public IEnumerable<int> Ids => Nodes.Select(n => n.Id).Concat(Edges.Select(e => e.Id)).Concat(Faces.Select(f => f.Id));

        /// <summary>The height of a story's floor plane in frame units.</summary>
        public int StoryFloor(int story)
        {
            int floor = Base;
            for (int s = 0; s < story && s < StoryHeights.Count; s++) floor += StoryHeights[s];
            return floor;
        }

        public bool TryGetNode(int id, out ShipFrameNode node)
        {
            int index = IndexOf(Nodes, id, n => n.Id);
            node = index >= 0 ? Nodes[index] : default;
            return index >= 0;
        }

        public bool TryGetEdge(int id, out ShipFrameEdge edge)
        {
            int index = IndexOf(Edges, id, e => e.Id);
            edge = index >= 0 ? Edges[index] : default;
            return index >= 0;
        }

        public bool TryGetFace(int id, out ShipFrameFace face)
        {
            int index = IndexOf(Faces, id, f => f.Id);
            face = index >= 0 ? Faces[index] : null!;
            return index >= 0;
        }

        /// <summary>The edge joining two nodes, if there is one.</summary>
        public bool TryGetEdgeBetween(int a, int b, out ShipFrameEdge edge)
        {
            foreach (var candidate in Edges)
            {
                if (!candidate.Joins(a, b)) continue;
                edge = candidate;
                return true;
            }

            edge = default;
            return false;
        }

        /// <summary>A node's ship-local position in metres: its story's floor plus its drift.</summary>
        public Vector3 Position(ShipFrameNode node) => new(
            ShipFrameUnits.ToMetres(node.X),
            ShipFrameUnits.ToMetres(StoryFloor(node.Story) + node.Y),
            ShipFrameUnits.ToMetres(node.Z));

        /// <summary>The ship-local position of a node by id (the origin for a node that is not there).</summary>
        public Vector3 Position(int nodeId) => TryGetNode(nodeId, out var node) ? Position(node) : Vector3.zero;

        public ShipFrame With(IEnumerable<ShipFrameNode>? nodes = null, IEnumerable<ShipFrameEdge>? edges = null,
            IEnumerable<ShipFrameFace>? faces = null, IEnumerable<int>? storyHeights = null) =>
            new(Base, storyHeights ?? StoryHeights, nodes ?? Nodes, edges ?? Edges, faces ?? Faces);

        public override string ToString() => $"frame ({StoryHeights.Count} stories, {Nodes.Count} nodes, {Edges.Count} edges, {Faces.Count} faces)";

        /// <summary>Binary search: the lists are sorted by id.</summary>
        private static int IndexOf<T>(IReadOnlyList<T> sorted, int id, Func<T, int> idOf)
        {
            int low = 0, high = sorted.Count - 1;
            while (low <= high)
            {
                int mid = (low + high) / 2;
                int at = idOf(sorted[mid]);
                if (at == id) return mid;
                if (at < id) low = mid + 1;
                else high = mid - 1;
            }

            return -1;
        }
    }
}
