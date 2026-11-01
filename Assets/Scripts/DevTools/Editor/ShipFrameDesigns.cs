#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Features.ShipDesigns;

namespace TinCan.DevTools.Editor
{
    /// <summary>
    /// The built-in designs, all frames with parts hung on them (.docs/plans/frame-only-ships.md), written by
    /// <see cref="ShipDesignsAssetBuilder"/>: the Starter sloop, the Galleon, the Raft and the Corsair. Positions are frame units (10
    /// cm), x across the ship (+ starboard), z along it (+ bow); every design's first story floor is at -0.5 m.
    /// </summary>
    public static class ShipFrameDesigns
    {
        public const string StarterName = "Starter";
        public const string GalleonName = "Galleon";
        public const string RaftName = "Raft";
        public const string CorsairName = "Corsair";

        /// <summary>
        /// The starter: a sloop 6 m wide and 11 m long, one railed deck with a pointed bow; the helm midships, the cairn
        /// forward, an engine at the stern, a mount plate to starboard, a mast on the bow node.
        /// </summary>
        public static ShipDesign StarterDesign()
        {
            var b = new FrameBuilder(-5, 30);
            int[] deck = { b.Node(0, -25, -45), b.Node(0, 25, -45), b.Node(0, 30, 20), b.Node(0, 15, 50), b.Node(0, 0, 65), b.Node(0, -15, 50), b.Node(0, -30, 20) };
            int floor = b.Face(deck);
            b.RailAround(deck);

            b.OnFace(ShipDesignsAssetBuilder.CoreHelm, floor, 0, -5);
            b.OnFace(ShipDesignsAssetBuilder.Cairn, floor, 0, 40);
            b.OnFace(ShipDesignsAssetBuilder.Engine, floor, 0, -35);
            b.OnFace(ShipDesignsAssetBuilder.HullMount, floor, 20, 10);
            b.OnNode(ShipDesignsAssetBuilder.Mast, deck[4]);
            return b.Build(StarterName);
        }

        /// <summary>
        /// The galleon: three stories at the old galleon's heights, so its spots keep their meaning. The hold (floor
        /// -3.5 m, 7 m wide) under the main deck (-0.5 m, 9 m wide, the planked hull walls between them leaning in), and the
        /// quarterdeck (2.5 m) over a cabin aft, walled at the sides and stern, open forward. A 3 x 5 m railed hatch in the
        /// main deck holds a flight down to the hold; two 1 m flights rise from the main deck to the quarterdeck's front,
        /// either side of the helm. A balcony off the quarterdeck's starboard side, through a gate. The cairn in the hold;
        /// mount plates on every level (the loadout's sockets: <see cref="GalleonMounts"/>); two propellers on the hold's
        /// stern corners.
        /// </summary>
        public static ShipDesign GalleonDesign() => Galleon(out _);

        /// <summary>The Galleon's mount plates by place, as their part instance ids (the socket ids its loadout names).</summary>
        public static GalleonMountIds GalleonMounts()
        {
            Galleon(out var mounts);
            return mounts;
        }

        public readonly struct GalleonMountIds
        {
            public GalleonMountIds(int holdPortAft, int holdStarboardAft, int holdPortFore, int holdStarboardFore, int bowPort, int bowStarboard,
                int sternPort, int sternStarboard, int balcony, int quarterdeckPortAft)
            {
                HoldPortAft = holdPortAft;
                HoldStarboardAft = holdStarboardAft;
                HoldPortFore = holdPortFore;
                HoldStarboardFore = holdStarboardFore;
                BowPort = bowPort;
                BowStarboard = bowStarboard;
                SternPort = sternPort;
                SternStarboard = sternStarboard;
                Balcony = balcony;
                QuarterdeckPortAft = quarterdeckPortAft;
            }

            public int HoldPortAft { get; }
            public int HoldStarboardAft { get; }
            public int HoldPortFore { get; }
            public int HoldStarboardFore { get; }
            public int BowPort { get; }
            public int BowStarboard { get; }
            public int SternPort { get; }
            public int SternStarboard { get; }
            public int Balcony { get; }
            public int QuarterdeckPortAft { get; }
        }

        private static ShipDesign Galleon(out GalleonMountIds mounts)
        {
            var b = new FrameBuilder(-35, 30, 30, 30);

            // The hold (story 0): stern port, stern starboard, then forward round the bow.
            int[] hold = { b.Node(0, -35, -75), b.Node(0, 35, -75), b.Node(0, 35, 55), b.Node(0, 20, 75), b.Node(0, 0, 95), b.Node(0, -20, 75), b.Node(0, -35, 55) };
            int holdFloor = b.Face(hold);

            // The main deck (story 1), node for node over the hold, with a node each side where the cabin ends (z -1.5 m).
            int m1 = b.Node(1, -45, -85), m2 = b.Node(1, 45, -85), m8 = b.Node(1, 45, -15), m3 = b.Node(1, 45, 60), m4 = b.Node(1, 25, 90),
                m5 = b.Node(1, 0, 115), m6 = b.Node(1, -25, 90), m7 = b.Node(1, -45, 60), m9 = b.Node(1, -45, -15);
            int[] deckOver = { m1, m2, m3, m4, m5, m6, m7 };
            for (int i = 0; i < hold.Length; i++) b.Edge(hold[i], deckOver[i]);

            // The hull walls, leaning out from the hold to the main deck.
            b.Face(hold[0], hold[1], m2, m1);
            b.Face(hold[1], hold[2], m3, m8, m2);
            b.Face(hold[2], hold[3], m4, m3);
            b.Face(hold[3], hold[4], m5, m4);
            b.Face(hold[4], hold[5], m6, m5);
            b.Face(hold[5], hold[6], m7, m6);
            b.Face(hold[6], hold[0], m1, m9, m7);

            // The hatch, and the main deck as five faces round it: the cabin floor, aft of the hatch, the two sides and the bow.
            int k1 = b.Node(1, -15, 5), k2 = b.Node(1, 15, 5), k3 = b.Node(1, 15, 55), k4 = b.Node(1, -15, 55);
            int cabinFloor = b.Face(m1, m2, m8, m9);
            b.Face(m9, m8, k2, k1);
            b.Face(m8, m3, k3, k2);
            int bow = b.Face(m3, m4, m5, m6, m7, k4, k3);
            b.Face(m7, m9, k1, k4);
            b.Rail(ShipFrameRailing.Rail, m8, m3, m4, m5, m6, m7, m9);
            b.Rail(ShipFrameRailing.Rail, k2, k3, k4, k1);
            b.Stair(k1, k2, 0, 1);

            // The quarterdeck (story 2) over the cabin: the front split for the two side flights, the starboard side for the
            // balcony's gate.
            int q1 = b.Node(2, -45, -85), q2 = b.Node(2, 45, -85), qe = b.Node(2, 45, -65), qf = b.Node(2, 45, -55), q3 = b.Node(2, 45, -15),
                qd = b.Node(2, 35, -15), qc = b.Node(2, 25, -15), qb = b.Node(2, -25, -15), qa = b.Node(2, -35, -15), q4 = b.Node(2, -45, -15);
            b.Edge(m1, q1);
            b.Edge(m2, q2);
            b.Edge(m8, q3);
            b.Edge(m9, q4);
            b.Face(m1, m2, q2, q1);
            b.Face(m2, m8, q3, qf, qe, q2);
            b.Face(m9, m1, q1, q4);
            int quarterdeck = b.Face(q1, q2, qe, qf, q3, qd, qc, qb, qa, q4);
            b.Rail(ShipFrameRailing.Rail, qf, q3, qd);
            b.Rail(ShipFrameRailing.Rail, qc, qb);
            b.Rail(ShipFrameRailing.Rail, qa, q4, q1, q2, qe);
            b.Rail(ShipFrameRailing.Gate, qe, qf);
            b.Stair(qc, qd, 0, 1);
            b.Stair(qa, qb, 0, 1);

            int b1 = b.Node(2, 55, -65), b2 = b.Node(2, 55, -55);
            int balcony = b.Face(qe, b1, b2, qf);
            b.Rail(ShipFrameRailing.Rail, qe, b1, b2, qf);

            b.OnFace(ShipDesignsAssetBuilder.CoreHelm, quarterdeck, 0, -50);
            b.OnFace(ShipDesignsAssetBuilder.Cairn, holdFloor, 0, -40);
            // The aft hold plates sit 1 m inboard of the walls and face inboard (quarter turns 1 and 3), so the wide fuel
            // fittings clear the leaning walls. The stern plates sit 4 m inside the stern rail: a cannon's seat is 1.8 m
            // behind it, and nearer the stern it hung past the rail.
            int holdPortAft = b.OnFace(ShipDesignsAssetBuilder.HullMount, holdFloor, -25, -50, 1);
            int holdStarboardAft = b.OnFace(ShipDesignsAssetBuilder.HullMount, holdFloor, 25, -50, 3);
            int holdPortFore = b.OnFace(ShipDesignsAssetBuilder.HullMount, holdFloor, -30, 30);
            int holdStarboardFore = b.OnFace(ShipDesignsAssetBuilder.HullMount, holdFloor, 30, 30);
            int bowPort = b.OnFace(ShipDesignsAssetBuilder.HullMount, bow, -30, 65);
            int bowStarboard = b.OnFace(ShipDesignsAssetBuilder.HullMount, bow, 30, 65);
            int sternPort = b.OnFace(ShipDesignsAssetBuilder.HullMount, quarterdeck, -30, -45);
            int sternStarboard = b.OnFace(ShipDesignsAssetBuilder.HullMount, quarterdeck, 30, -45);
            int balconyMount = b.OnFace(ShipDesignsAssetBuilder.HullMount, balcony, 50, -60, 1);
            b.OnFace(ShipDesignsAssetBuilder.HullMount, cabinFloor, -30, -60);
            b.OnNode(ShipDesignsAssetBuilder.Propeller, hold[0]);
            b.OnNode(ShipDesignsAssetBuilder.Propeller, hold[1]);
            // Added after the rest, so every earlier placement keeps its id: the quarterdeck's port stern corner, under
            // open sky (the net rack's 5 m pole would pierce the quarterdeck from the cabin floor).
            int quarterdeckPortAft = b.OnFace(ShipDesignsAssetBuilder.HullMount, quarterdeck, -30, -75);
            mounts = new GalleonMountIds(holdPortAft, holdStarboardAft, holdPortFore, holdStarboardFore, bowPort, bowStarboard,
                sternPort, sternStarboard, balconyMount, quarterdeckPortAft);
            return b.Build(GalleonName);
        }

        /// <summary>The raft's 12 outline nodes, seen from above in frame units (x, z), bow at +Z: 8.4 m wide, 18.2 m long.</summary>
        private static readonly (int X, int Drift, int Z)[] Outline =
        {
            (0, 2, 100), (25, 1, 80), (40, 0, 45), (42, 0, 0), (40, 0, -40), (28, 0, -70),
            (0, 1, -82), (-28, 0, -70), (-40, 0, -40), (-42, 0, 0), (-40, 0, 45), (-25, 1, 80),
        };

        /// <summary>
        /// The first frame ship: one story, an irregular 12-node deck outline with a pointed bow (rising 0.2 m) and a rounded
        /// stern, planked as three faces (bow, waist, stern) split by beams across at z +4.5 m and -4 m, its top at -0.5 m.
        /// The helm midships, the cairn on the bow, an engine at the stern. Ids: nodes 1-12, outline edges 13-24, cross
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
            var frame = new ShipFrame(-5, new[] { 30 }, nodes, edges, faces);

            var parts = new[]
            {
                new ShipPartPlacement(30, ShipDesignsAssetBuilder.CoreHelm, ShipPartAnchor.OnFace(28, 0, 0), 0),
                new ShipPartPlacement(31, ShipDesignsAssetBuilder.Engine, ShipPartAnchor.OnFace(29, 0, -55), 0),
                new ShipPartPlacement(32, ShipDesignsAssetBuilder.Cairn, ShipPartAnchor.OnFace(27, 0, 60), 0),
            };
            return new ShipDesign(RaftName, "TinCan", 33, parts, frame: frame);
        }

        /// <summary>The Corsair's hold floor, seen from above in frame units (x, z), stern port first, then round by the bow.</summary>
        private static readonly (int X, int Z)[] CorsairHold =
        {
            (-18, -120), (18, -120), (22, -90), (24, -30), (22, 40), (16, 80), (5, 105), (-5, 105), (-16, 80), (-22, 40), (-24, -30), (-22, -90),
        };

        /// <summary>The Corsair's chine, node for node round the hold, where the bilge meets the hull walls.</summary>
        private static readonly (int X, int Z)[] CorsairChine =
        {
            (-26, -132), (26, -132), (34, -96), (36, -30), (34, 40), (25, 86), (7, 120), (-7, 120), (-25, 86), (-34, 40), (-36, -30), (-34, -96),
        };

        /// <summary>The Corsair's main deck, node for node over the chine; the bow sheers up (drift).</summary>
        private static readonly (int X, int Z, int Drift)[] CorsairDeck =
        {
            (-30, -140, 0), (30, -140, 0), (40, -100, 0), (42, -30, 0), (40, 40, 0), (30, 90, 2), (8, 130, 5), (-8, 130, 5), (-30, 90, 2),
            (-40, 40, 0), (-42, -30, 0), (-40, -100, 0),
        };

        /// <summary>
        /// The Corsair, after a sky-galleon concept: a round-bellied hull 8.4 m wide and 35 m long with its bowsprit, four
        /// stories. The keel story (floor -3.5 m) is the hold with a bilge rising 0.4 m all round it to the chine, so the hull
        /// rounds under; the walls lean out from the chine to the main deck (-0.5 m), whose bow sheers up into a railed
        /// bowsprit. A railed hatch with a flight to the hold; a cabin aft under the quarterdeck (2.5 m), two flights up to it
        /// either side; on the quarterdeck the stern castle, walled at the sides and stern, open forward, a flight from its
        /// roof (5.5 m), where the helm stands. Three rotor masts on the centreline (bow, waist, the castle's stern), two
        /// propellers on the stern chine, an engine and the cairn in the hold, four mount plates (bow and quarterdeck).
        /// </summary>
        public static ShipDesign CorsairDesign()
        {
            var b = new FrameBuilder(-35, 30, 30, 30, 30);
            int n = CorsairHold.Length;

            // The keel story (0): the hold floor, and the bilge round it up to the chine.
            int[] hold = CorsairHold.Select(p => b.Node(0, p.X, p.Z)).ToArray();
            int[] chine = CorsairChine.Select(p => b.Node(0, p.X, p.Z, 4)).ToArray();
            int holdFloor = b.Face(hold);
            for (int i = 0; i < n; i++) b.Face(hold[i], hold[(i + 1) % n], chine[(i + 1) % n], chine[i]);

            // The main deck (story 1) and the hull walls up to it from the chine.
            int[] d = CorsairDeck.Select(p => b.Node(1, p.X, p.Z, p.Drift)).ToArray();
            for (int i = 0; i < n; i++) b.Face(chine[i], chine[(i + 1) % n], d[(i + 1) % n], d[i]);

            // The deck round the hatch: the cabin floor, a strip aft of the hatch, the two sides, the waist (its forward
            // edge a beam across, with the fore mast's node), the bow and the bowsprit.
            int k1 = b.Node(1, -15, -20), k2 = b.Node(1, 15, -20), k3 = b.Node(1, 15, 30), kc = b.Node(1, 0, 30), k4 = b.Node(1, -15, 30);
            int fore = b.Node(1, 0, 95, 3);
            int sprit = b.Node(1, 0, 210, 8);
            b.Face(d[0], d[1], d[2], d[3], d[10], d[11]);
            b.Face(d[10], d[3], k2, k1);
            b.Face(d[3], d[4], k3, k2);
            int waist = b.Face(d[4], d[5], fore, d[8], d[9], k4, kc, k3);
            b.Face(d[5], d[6], d[7], d[8], fore);
            b.Face(d[9], d[10], k1, k4);
            b.Face(d[6], sprit, d[7]);
            b.Rail(ShipFrameRailing.Rail, d[3], d[4], d[5], d[6], sprit, d[7], d[8], d[9], d[10]);
            b.Rail(ShipFrameRailing.Rail, k2, k3, kc, k4, k1);
            b.Stair(k1, k2, 0, 1);

            // The quarterdeck (story 2) over the cabin, its front split for the two flights down to the main deck.
            int q0 = b.Node(2, -30, -140), q1 = b.Node(2, 30, -140), q2 = b.Node(2, 40, -100), q3 = b.Node(2, 42, -30), qd = b.Node(2, 35, -30),
                qc = b.Node(2, 25, -30), qb = b.Node(2, -25, -30), qa = b.Node(2, -35, -30), q10 = b.Node(2, -42, -30), q11 = b.Node(2, -40, -100);
            b.Face(d[0], d[1], q1, q0);
            b.Face(d[1], d[2], q2, q1);
            b.Face(d[2], d[3], q3, q2);
            b.Face(d[11], d[0], q0, q11);
            b.Face(d[10], d[11], q11, q10);
            int quarterdeck = b.Face(q0, q1, q2, q3, qd, qc, qb, qa, q10, q11);
            b.Rail(ShipFrameRailing.Rail, qd, q3, q2, q1);
            b.Rail(ShipFrameRailing.Rail, qb, qc);
            b.Rail(ShipFrameRailing.Rail, q0, q11, q10, qa);
            b.Stair(qa, qb, 0, 1);
            b.Stair(qc, qd, 0, 1);

            // The stern castle on the quarterdeck: walls at the stern (over the cabin's) and sides, its roof (story 3) railed
            // but for a flight down to the quarterdeck at its port front.
            int t2 = b.Node(2, 30, -85), t3 = b.Node(2, -30, -85);
            int r0 = b.Node(3, -30, -140), rm = b.Node(3, 0, -140), r1 = b.Node(3, 30, -140), r2 = b.Node(3, 30, -85), rs = b.Node(3, -12, -85),
                r3 = b.Node(3, -30, -85);
            b.Face(q0, q1, r1, rm, r0);
            b.Face(q1, t2, r2, r1);
            b.Face(t3, q0, r0, r3);
            int castle = b.Face(r0, rm, r1, r2, rs, r3);
            b.Rail(ShipFrameRailing.Rail, rs, r2, r1, rm, r0, r3);
            b.Stair(r3, rs, 0, 1);

            b.OnFace(ShipDesignsAssetBuilder.CoreHelm, castle, 0, -110);
            b.OnFace(ShipDesignsAssetBuilder.Cairn, holdFloor, 0, -60);
            b.OnFace(ShipDesignsAssetBuilder.Engine, holdFloor, 0, -95);
            b.OnNode(ShipDesignsAssetBuilder.Propeller, chine[0]);
            b.OnNode(ShipDesignsAssetBuilder.Propeller, chine[1]);
            b.OnNode(ShipDesignsAssetBuilder.RotorMast, fore);
            b.OnNode(ShipDesignsAssetBuilder.RotorMast, kc);
            b.OnNode(ShipDesignsAssetBuilder.RotorMast, rm);
            b.OnFace(ShipDesignsAssetBuilder.HullMount, waist, -28, 60);
            b.OnFace(ShipDesignsAssetBuilder.HullMount, waist, 28, 60);
            b.OnFace(ShipDesignsAssetBuilder.HullMount, quarterdeck, -36, -60);
            b.OnFace(ShipDesignsAssetBuilder.HullMount, quarterdeck, 34, -60);
            return b.Build(CorsairName);
        }

        /// <summary>
        /// Writes a frame and its parts with ids in order: each node, edge, face and flight as it is added, then the parts.
        /// A face adds the edges round it that are missing.
        /// </summary>
        private sealed class FrameBuilder
        {
            private readonly int _base;
            private readonly int[] _stories;
            private readonly List<ShipFrameNode> _nodes = new();
            private readonly List<ShipFrameEdge> _edges = new();
            private readonly List<ShipFrameFace> _faces = new();
            private readonly List<ShipFrameStair> _stairs = new();
            private readonly List<ShipPartPlacement> _parts = new();
            private int _next = 1;

            public FrameBuilder(int @base, params int[] stories)
            {
                _base = @base;
                _stories = stories;
            }

            public int Node(int story, int x, int z, int drift = 0)
            {
                _nodes.Add(new ShipFrameNode(_next, story, x, drift, z));
                return _next++;
            }

            public int Edge(int a, int b)
            {
                var existing = _edges.FindIndex(e => e.Joins(a, b));
                if (existing >= 0) return _edges[existing].Id;
                _edges.Add(new ShipFrameEdge(_next, a, b));
                return _next++;
            }

            /// <summary>A planked face round these nodes, with the edges between them.</summary>
            public int Face(params int[] loop)
            {
                for (int i = 0; i < loop.Length; i++) Edge(loop[i], loop[(i + 1) % loop.Length]);
                _faces.Add(new ShipFrameFace(_next, loop, ShipFrameFill.Planks));
                return _next++;
            }

            /// <summary>Railings (or gates) along the edges from node to node in this order.</summary>
            public void Rail(ShipFrameRailing rail, params int[] path)
            {
                for (int i = 0; i + 1 < path.Length; i++)
                {
                    int id = Edge(path[i], path[i + 1]);
                    int index = _edges.FindIndex(e => e.Id == id);
                    _edges[index] = _edges[index].WithRail(rail);
                }
            }

            /// <summary>Railings all round a closed outline.</summary>
            public void RailAround(int[] loop) => Rail(ShipFrameRailing.Rail, loop.Append(loop[0]).ToArray());

            /// <summary>A flight hanging from the edge a-b, running down towards (dx, dz), whichever way the edge is stored.</summary>
            public void Stair(int a, int b, int dx, int dz)
            {
                var edge = _edges.First(e => e.Id == Edge(a, b));
                var from = _nodes.First(n => n.Id == edge.A);
                var to = _nodes.First(n => n.Id == edge.B);
                int alongX = to.X - from.X, alongZ = to.Z - from.Z;
                // The flight's side +1 is the left of A to B seen from above: (-along z, along x).
                int dir = -alongZ * dx + alongX * dz >= 0 ? 1 : -1;
                _stairs.Add(new ShipFrameStair(_next, edge.Id, dir));
                _next++;
            }

            public int OnFace(string part, int face, int x, int z, byte yaw = 0) => Part(part, ShipPartAnchor.OnFace(face, x, z), yaw);

            public int OnNode(string part, int node, byte yaw = 0) => Part(part, ShipPartAnchor.OnNode(node), yaw);

            public ShipDesign Build(string name) =>
                new(name, "TinCan", _next, _parts, frame: new ShipFrame(_base, _stories, _nodes, _edges, _faces, _stairs));

            private int Part(string part, ShipPartAnchor anchor, byte yaw)
            {
                _parts.Add(new ShipPartPlacement(_next, part, anchor, yaw));
                return _next++;
            }
        }
    }
}
