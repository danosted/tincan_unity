#nullable enable
using System.Linq;
using TinCan.Features.ShipDesigns;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Small framed designs for tests (with <see cref="ShipDesignTestParts"/>): <see cref="Deck"/> is a 4 x 4 m plank deck
    /// on one 3 m story whose floor is at -0.5 m, its +X/+Z corner drifted 0.2 m up and a railing on its +Z side, with
    /// the helm standing on it in the middle. Ids: nodes 1-4, edges 5-8, the face 9, the helm 10. The v2 and v3 golden
    /// files are this design in those formats.
    /// </summary>
    public static class ShipFrameTestDesigns
    {
        public const int NextId = 11;
        public const int Base = -5;
        public const int StoryHeight = 30;

        public static ShipFrame DeckFrame(ShipFrameFill fill = ShipFrameFill.Planks) => new(Base, new[] { StoryHeight },
            new[]
            {
                new ShipFrameNode(1, 0, -20, 0, -20), new ShipFrameNode(2, 0, -20, 0, 20),
                new ShipFrameNode(3, 0, 20, 2, 20), new ShipFrameNode(4, 0, 20, 0, -20),
            },
            new[] { new ShipFrameEdge(5, 1, 2), new ShipFrameEdge(6, 2, 3, ShipFrameRailing.Rail), new ShipFrameEdge(7, 3, 4), new ShipFrameEdge(8, 4, 1) },
            new[] { new ShipFrameFace(9, new[] { 1, 2, 3, 4 }, fill) });

        public static ShipDesign Deck(ShipFrameFill fill = ShipFrameFill.Planks) =>
            new("Golden Frame", "Tests", NextId,
                new[] { new ShipPartPlacement(10, ShipDesignTestParts.Helm, ShipPartAnchor.OnFace(FloorFace, 0, 0), 0) }, frame: DeckFrame(fill));

        /// <summary>The face every one-floor test design has: <see cref="DeckFrame"/>'s, and <see cref="Floor"/>'s.</summary>
        public const int FloorFace = 9;

        /// <summary>A flat 20 x 20 m plank floor at -0.5 m (nodes 1-4, edges 5-8, the face 9): room for any test layout, light enough to fly.</summary>
        public static ShipFrame Floor() => new(Base, new[] { StoryHeight },
            new[]
            {
                new ShipFrameNode(1, 0, -100, 0, -100), new ShipFrameNode(2, 0, -100, 0, 100),
                new ShipFrameNode(3, 0, 100, 0, 100), new ShipFrameNode(4, 0, 100, 0, -100),
            },
            new[] { new ShipFrameEdge(5, 1, 2), new ShipFrameEdge(6, 2, 3), new ShipFrameEdge(7, 3, 4), new ShipFrameEdge(8, 4, 1) },
            new[] { new ShipFrameFace(FloorFace, new[] { 1, 2, 3, 4 }, ShipFrameFill.Planks) });

        /// <summary>
        /// Two stories of a 4 x 4 m box: lower corners 1-4 and upper corners 5-8 (straight above), floor edges 9-12 and
        /// 13-16, posts 17-20, the two plank floors 21 and 22 and the four planked walls 23-26. The floors are
        /// <paramref name="storyHeight"/> frame units apart; the lower floor is at -0.5 m.
        /// </summary>
        public static ShipFrame TwoStoryBox(int storyHeight = StoryHeight)
        {
            var corners = new[] { (-20, -20), (-20, 20), (20, 20), (20, -20) };
            var nodes = corners.Select((c, i) => new ShipFrameNode(i + 1, 0, c.Item1, 0, c.Item2))
                .Concat(corners.Select((c, i) => new ShipFrameNode(i + 5, 1, c.Item1, 0, c.Item2)));
            var edges = Enumerable.Range(0, 4).Select(i => new ShipFrameEdge(9 + i, i + 1, (i + 1) % 4 + 1))
                .Concat(Enumerable.Range(0, 4).Select(i => new ShipFrameEdge(13 + i, i + 5, (i + 1) % 4 + 5)))
                .Concat(Enumerable.Range(0, 4).Select(i => new ShipFrameEdge(17 + i, i + 1, i + 5)));
            var faces = new[]
                {
                    new ShipFrameFace(21, new[] { 1, 2, 3, 4 }, ShipFrameFill.Planks),
                    new ShipFrameFace(22, new[] { 5, 6, 7, 8 }, ShipFrameFill.Planks),
                }
                .Concat(Enumerable.Range(0, 4).Select(i => new ShipFrameFace(23 + i,
                    new[] { i + 1, (i + 1) % 4 + 1, (i + 1) % 4 + 5, i + 5 }, ShipFrameFill.Planks)));
            return new ShipFrame(Base, new[] { storyHeight, storyHeight }, nodes, edges, faces);
        }

        /// <summary>
        /// Two stories: a 4 x 10 m lower floor (nodes 1-4, from z -4 to 6 m) and a 4 x 4 m upper floor over its aft part (5-8,
        /// to z 0), joined by two posts at the stern (17, 18). A flight (21) hangs from the upper floor's forward edge (15, from
        /// node 7 to 8, so its left is aft and it runs forward on side -1) down to the lower floor, landing at z 5 m. Faces 19
        /// and 20.
        /// </summary>
        public static ShipFrame StairWell() => new(Base, new[] { StoryHeight, StoryHeight },
            new[]
            {
                new ShipFrameNode(1, 0, -20, 0, -40), new ShipFrameNode(2, 0, 20, 0, -40), new ShipFrameNode(3, 0, 20, 0, 60), new ShipFrameNode(4, 0, -20, 0, 60),
                new ShipFrameNode(5, 1, -20, 0, -40), new ShipFrameNode(6, 1, 20, 0, -40), new ShipFrameNode(7, 1, 20, 0, 0), new ShipFrameNode(8, 1, -20, 0, 0),
            },
            new[]
            {
                new ShipFrameEdge(9, 1, 2), new ShipFrameEdge(10, 2, 3), new ShipFrameEdge(11, 3, 4), new ShipFrameEdge(12, 4, 1),
                new ShipFrameEdge(13, 5, 6), new ShipFrameEdge(14, 6, 7), new ShipFrameEdge(15, 7, 8), new ShipFrameEdge(16, 8, 5),
                new ShipFrameEdge(17, 1, 5), new ShipFrameEdge(18, 2, 6),
            },
            new[] { new ShipFrameFace(19, new[] { 1, 2, 3, 4 }, ShipFrameFill.Planks), new ShipFrameFace(20, new[] { 5, 6, 7, 8 }, ShipFrameFill.Planks) },
            new[] { new ShipFrameStair(21, 15, -1) });

        /// <summary>A design with just a frame (no parts), its next id one past the highest.</summary>
        public static ShipDesign FrameOnly(ShipFrame frame) =>
            new("Frame", "Tests", frame.ElementCount == 0 ? 1 : frame.Ids.Max() + 1, System.Array.Empty<ShipPartPlacement>(), frame: frame);
    }
}
