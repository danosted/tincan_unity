#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Features.ShipDesigns;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// Built-in designs with a frame (.docs/plans/ship-frames.md), written by <see cref="ShipDesignsAssetBuilder"/>.
    /// </summary>
    public static class ShipFrameDesigns
    {
        public const string RaftName = "Raft";
        public const string FrameGalleonName = "FrameGalleon";

        /// <summary>The frame galleon's main deck outline (story 1) and the hold's (story 0), node for node, in frame units (x, z).</summary>
        private static readonly (int X, int Z)[] MainDeck =
        {
            (0, 90), (30, 70), (45, 40), (45, -45), (30, -80), (-30, -80), (-45, -45), (-45, 40), (-30, 70),
        };

        private static readonly (int X, int Z)[] Hold =
        {
            (0, 70), (20, 55), (30, 30), (30, -40), (20, -70), (-20, -70), (-30, -40), (-30, 30), (-20, 55),
        };

        /// <summary>The hatch in the main deck: aft port, aft starboard, fore starboard, fore port (3 m wide, 5 m long).</summary>
        private static readonly (int X, int Z)[] Hatch = { (-15, -35), (15, -35), (15, 15), (-15, 15) };

        /// <summary>
        /// The frame galleon (S3): two stories. The hold (story 0, floor at -3.5 m) is narrower than the main deck (story 1,
        /// floor at -0.5 m, the floor of grid cell y 0), so the planked hull walls between them lean in like a keel. The
        /// main deck is four plank faces around an open hatch, railed all round and on three sides of the hatch; three
        /// stairs rise through the hatch from the hold, landing forward of it. On deck: the helm forward, two engines
        /// aft, four masts carrying the envelope, a balloon off each side. Ids: hold nodes 1-9, deck nodes 10-18, hatch
        /// 19-22, edges 23-57, faces 58-71, parts from 72. Plan: .docs/plans/ship-frames.md.
        /// </summary>
        public static ShipDesign FrameGalleonDesign()
        {
            var nodes = new List<ShipFrameNode>();
            nodes.AddRange(Hold.Select((p, i) => new ShipFrameNode(1 + i, 0, p.X, 0, p.Z)));
            nodes.AddRange(MainDeck.Select((p, i) => new ShipFrameNode(10 + i, 1, p.X, i == 0 ? 2 : 0, p.Z)));
            nodes.AddRange(Hatch.Select((p, i) => new ShipFrameNode(19 + i, 1, p.X, 0, p.Z)));
            const int a = 19, b = 20, c = 21, d = 22;
            int M(int i) => 9 + i;
            int H(int i) => i;

            var edges = new List<ShipFrameEdge>();
            for (int i = 0; i < 9; i++) edges.Add(new ShipFrameEdge(23 + i, H(i + 1), H(i == 8 ? 1 : i + 2)));
            for (int i = 0; i < 9; i++) edges.Add(new ShipFrameEdge(32 + i, M(i + 1), M(i == 8 ? 1 : i + 2), railing: true));
            for (int i = 0; i < 9; i++) edges.Add(new ShipFrameEdge(41 + i, H(i + 1), M(i + 1)));
            edges.Add(new ShipFrameEdge(50, a, b, railing: true));
            edges.Add(new ShipFrameEdge(51, b, c, railing: true));
            edges.Add(new ShipFrameEdge(52, c, d));
            edges.Add(new ShipFrameEdge(53, d, a, railing: true));
            edges.Add(new ShipFrameEdge(54, c, M(3)));
            edges.Add(new ShipFrameEdge(55, d, M(8)));
            edges.Add(new ShipFrameEdge(56, b, M(4)));
            edges.Add(new ShipFrameEdge(57, a, M(7)));

            var faces = new List<ShipFrameFace>
            {
                new(58, Enumerable.Range(1, 9).Select(H).ToList(), ShipFrameFill.Planks),
                new(59, new[] { d, c, M(3), M(2), M(1), M(9), M(8) }, ShipFrameFill.Planks),
                new(60, new[] { c, b, M(4), M(3) }, ShipFrameFill.Planks),
                new(61, new[] { b, a, M(7), M(6), M(5), M(4) }, ShipFrameFill.Planks),
                new(62, new[] { a, d, M(8), M(7) }, ShipFrameFill.Planks),
            };
            for (int i = 1; i <= 9; i++)
            {
                int next = i == 9 ? 1 : i + 1;
                faces.Add(new ShipFrameFace(62 + i, new[] { H(i), H(next), M(next), M(i) }, ShipFrameFill.Planks));
            }

            var frame = new ShipFrame(-35, new[] { 30, 30 }, nodes, edges, faces);

            var parts = new List<(string Part, ShipGridCell Cell)>
            {
                (ShipDesignsAssetBuilder.CoreHelm, new ShipGridCell(0, 0, 5)),
                (ShipDesignsAssetBuilder.Engine, new ShipGridCell(-1, 0, -6)),
                (ShipDesignsAssetBuilder.Engine, new ShipGridCell(1, 0, -6)),
                (ShipDesignsAssetBuilder.Balloon, new ShipGridCell(-6, 0, -1)),
                (ShipDesignsAssetBuilder.Balloon, new ShipGridCell(6, 0, -1)),
            };
            foreach (int x in new[] { -2, 2 })
            foreach (int z in new[] { -2, 2 })
            for (int y = 0; y <= 2; y++)
                parts.Add((ShipDesignsAssetBuilder.Mast, new ShipGridCell(x, y, z)));
            parts.Add((ShipDesignsAssetBuilder.Envelope, new ShipGridCell(0, 3, 0)));
            // Stairs rise +Z from the hold floor (cell y -3) through the hatch to the main deck, landing at cell z 2.
            foreach (int x in new[] { -1, 0, 1 }) parts.Add((ShipDesignsAssetBuilder.Stairs, new ShipGridCell(x, -3, -3)));

            var placements = parts.Select((p, i) => new ShipPartPlacement(72 + i, p.Part, p.Cell, 0)).ToList();
            return new ShipDesign(FrameGalleonName, "TinCan", 72 + placements.Count, placements, frame: frame);
        }

        /// <summary>The raft's 12 outline nodes, seen from above in frame units (x, z), bow at +Z: 8.4 m wide, 18.2 m long.</summary>
        private static readonly (int X, int Drift, int Z)[] Outline =
        {
            (0, 2, 100), (25, 1, 80), (40, 0, 45), (42, 0, 0), (40, 0, -40), (28, 0, -70),
            (0, 1, -82), (-28, 0, -70), (-40, 0, -40), (-42, 0, 0), (-40, 0, 45), (-25, 1, 80),
        };

        /// <summary>
        /// The first frame ship: one story, an irregular 12-node deck outline with a pointed bow (rising 0.2 m) and a rounded
        /// stern, planked as three faces (bow, waist, stern) split by beams across at z +4.5 m and -4 m. The deck's top is at
        /// -0.5 m, the floor of grid cell y 0, so parts stand on it as on deck tiles: the helm midships, an engine at the
        /// stern, four three-cell masts holding the envelope above the waist. Ids: nodes 1-12, outline edges 13-24, cross
        /// beams 25-26, faces 27-29, parts from 30.
        /// </summary>
        public static ShipDesign RaftDesign()
        {
            var nodes = Outline.Select((p, i) => new ShipFrameNode(i + 1, 0, p.X, p.Drift, p.Z)).ToList();
            var edges = Enumerable.Range(0, 12).Select(i => new ShipFrameEdge(13 + i, i + 1, (i + 1) % 12 + 1)).ToList();
            edges.Add(new ShipFrameEdge(25, 3, 11));
            edges.Add(new ShipFrameEdge(26, 5, 9));
            var faces = new[]
            {
                new ShipFrameFace(27, new[] { 1, 2, 3, 11, 12 }, ShipFrameFill.Planks),
                new ShipFrameFace(28, new[] { 11, 3, 4, 5, 9, 10 }, ShipFrameFill.Planks),
                new ShipFrameFace(29, new[] { 9, 5, 6, 7, 8 }, ShipFrameFill.Planks),
            };
            var frame = new ShipFrame(-ShipFrameUnits.PerCell / 2, new[] { 3 * ShipFrameUnits.PerCell }, nodes, edges, faces);

            var parts = new List<(string Part, ShipGridCell Cell)>
            {
                (ShipDesignsAssetBuilder.CoreHelm, new ShipGridCell(0, 0, 0)),
                (ShipDesignsAssetBuilder.Engine, new ShipGridCell(0, 0, -6)),
            };
            foreach (int x in new[] { -2, 2 })
            foreach (int z in new[] { -2, 2 })
            for (int y = 0; y <= 2; y++)
                parts.Add((ShipDesignsAssetBuilder.Mast, new ShipGridCell(x, y, z)));
            parts.Add((ShipDesignsAssetBuilder.Envelope, new ShipGridCell(0, 3, 0)));

            var placements = parts.Select((p, i) => new ShipPartPlacement(30 + i, p.Part, p.Cell, 0)).ToList();
            return new ShipDesign(RaftName, "TinCan", 30 + placements.Count, placements, frame: frame);
        }
    }
}
