#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// One change to a design's frame, applied by <see cref="ShipFrameEditProcessor"/>. Positions are in frame units;
    /// <see cref="Id"/> and <see cref="Other"/> are the node, edge or face ids the edit concerns. Plan:
    /// .docs/plans/ship-frames.md.
    /// </summary>
    public sealed class ShipFrameEdit
    {
        private ShipFrameEdit(ShipFrameEditKind kind, int id = 0, int other = 0, int story = 0, int x = 0, int y = 0, int z = 0,
            IReadOnlyList<int>? nodes = null, ShipFrameFill fill = ShipFrameFill.Open, ShipFrameRailing rail = ShipFrameRailing.None,
            ShipFrame? frame = null)
        {
            Kind = kind;
            Id = id;
            Other = other;
            Story = story;
            X = x;
            Y = y;
            Z = z;
            Nodes = nodes ?? Array.Empty<int>();
            Fill = fill;
            Rail = rail;
            Frame = frame;
        }

        public ShipFrameEditKind Kind { get; }
        public int Id { get; }
        public int Other { get; }
        public int Story { get; }
        public int X { get; }
        public int Y { get; }
        public int Z { get; }
        public IReadOnlyList<int> Nodes { get; }
        public ShipFrameFill Fill { get; }
        public ShipFrameRailing Rail { get; }

        /// <summary>The frame a <see cref="ShipFrameEditKind.Restore"/> puts back.</summary>
        public ShipFrame? Frame { get; }

        public static ShipFrameEdit AddStory(int height) => new(ShipFrameEditKind.AddStory, story: height);

        public static ShipFrameEdit AddNode(int story, int x, int y, int z) => new(ShipFrameEditKind.AddNode, story: story, x: x, y: y, z: z);

        public static ShipFrameEdit MoveNode(int node, int x, int y, int z) => new(ShipFrameEditKind.MoveNode, node, x: x, y: y, z: z);

        public static ShipFrameEdit RemoveNode(int node) => new(ShipFrameEditKind.RemoveNode, node);

        public static ShipFrameEdit MergeNodes(int keep, int gone) => new(ShipFrameEditKind.MergeNodes, keep, gone);

        public static ShipFrameEdit AddEdge(int a, int b) => new(ShipFrameEditKind.AddEdge, a, b);

        public static ShipFrameEdit RemoveEdge(int edge) => new(ShipFrameEditKind.RemoveEdge, edge);

        public static ShipFrameEdit SplitEdge(int edge, int x, int y, int z) => new(ShipFrameEditKind.SplitEdge, edge, x: x, y: y, z: z);

        public static ShipFrameEdit SetRailing(int edge, ShipFrameRailing rail) => new(ShipFrameEditKind.SetRailing, edge, rail: rail);

        /// <summary>Stairs hanging from a floor edge, down into the story below, on the side <paramref name="dir"/> says (+1 or -1).</summary>
        public static ShipFrameEdit AddStair(int edge, int dir) => new(ShipFrameEditKind.AddStair, edge, dir);

        public static ShipFrameEdit FlipStair(int stair) => new(ShipFrameEditKind.FlipStair, stair);

        public static ShipFrameEdit RemoveStair(int stair) => new(ShipFrameEditKind.RemoveStair, stair);

        public static ShipFrameEdit AddFace(IEnumerable<int> nodes, ShipFrameFill fill) => new(ShipFrameEditKind.AddFace, nodes: nodes.ToList(), fill: fill);

        public static ShipFrameEdit SplitFace(int face, int a, int b) => new(ShipFrameEditKind.SplitFace, face, nodes: new[] { a, b });

        public static ShipFrameEdit SetFaceFill(int face, ShipFrameFill fill) => new(ShipFrameEditKind.SetFaceFill, face, fill: fill);

        public static ShipFrameEdit RemoveFace(int face) => new(ShipFrameEditKind.RemoveFace, face);

        public static ShipFrameEdit Restore(ShipFrame frame) => new(ShipFrameEditKind.Restore, frame: frame);

        public override string ToString() => Kind switch
        {
            ShipFrameEditKind.AddStory => $"Add a story {Story} high",
            ShipFrameEditKind.AddNode => $"Add a node on story {Story} at ({X}, {Y}, {Z})",
            ShipFrameEditKind.MoveNode or ShipFrameEditKind.SplitEdge => $"{Kind} #{Id} to ({X}, {Y}, {Z})",
            ShipFrameEditKind.MergeNodes => $"Merge node #{Other} into #{Id}",
            ShipFrameEditKind.AddEdge => $"Add an edge {Id}-{Other}",
            ShipFrameEditKind.SetRailing => $"Railing on edge #{Id}: {Rail}",
            ShipFrameEditKind.AddStair => $"Add stairs from edge #{Id}, side {Other}",
            ShipFrameEditKind.AddFace => $"Add a face {string.Join("-", Nodes)} ({Fill})",
            ShipFrameEditKind.SplitFace => $"Split face #{Id} along {string.Join("-", Nodes)}",
            ShipFrameEditKind.SetFaceFill => $"Fill face #{Id} with {Fill}",
            ShipFrameEditKind.Restore => $"Restore the {Frame}",
            _ => $"{Kind} #{Id}",
        };
    }
}
