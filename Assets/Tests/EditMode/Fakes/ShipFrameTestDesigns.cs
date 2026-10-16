#nullable enable
using System.Linq;
using TinCan.Features.ShipDesigns;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Small framed designs for tests (with <see cref="ShipDesignTestParts"/>): <see cref="Deck"/> is a 4 x 4 m plank deck
    /// on one 3 m story whose floor is at -0.5 m (the underside of grid cell y 0), its +X/+Z corner drifted 0.2 m up and a
    /// railing on its +Z side, with the helm standing on it. Ids: nodes 1-4, edges 5-8, the face 9, the helm 10. The
    /// v2 golden file is this design.
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
            new[] { new ShipFrameEdge(5, 1, 2), new ShipFrameEdge(6, 2, 3, railing: true), new ShipFrameEdge(7, 3, 4), new ShipFrameEdge(8, 4, 1) },
            new[] { new ShipFrameFace(9, new[] { 1, 2, 3, 4 }, fill) });

        public static ShipDesign Deck(ShipFrameFill fill = ShipFrameFill.Planks) =>
            new("Golden Frame", "Tests", NextId,
                new[] { new ShipPartPlacement(10, ShipDesignTestParts.Helm, new ShipGridCell(0, 0, 0), 0) }, frame: DeckFrame(fill));

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

        /// <summary>A design with just a frame (no parts), its next id one past the highest.</summary>
        public static ShipDesign FrameOnly(ShipFrame frame) =>
            new("Frame", "Tests", frame.ElementCount == 0 ? 1 : frame.Ids.Max() + 1, System.Array.Empty<ShipPartPlacement>(), frame: frame);
    }
}
