#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Frames with more than one story (.docs/plans/ship-frames.md, S3): wall faces, railings, headroom; their meshes;
    /// and the shipyard's frame tools making posts and closing walls.
    /// </summary>
    public class ShipFrameStoriesTests
    {
        private static ShipDesignValidation Validate(ShipFrame frame) =>
            new ShipDesignValidator().Validate(ShipFrameTestDesigns.FrameOnly(frame), new ShipPartCatalog(System.Array.Empty<TinCan.Core.Ship.Parts.ShipPartDefinition>()),
                ShipDesignLimits.Default);

        private static bool HasFrameProblem(ShipDesignValidation validation) => validation.Problems.Any(p =>
            p.Kind is ShipDesignProblemKind.BadFace or ShipDesignProblemKind.BadEdge or ShipDesignProblemKind.FrameReference
                or ShipDesignProblemKind.DanglingNode or ShipDesignProblemKind.FloorTooSteep);

        [Test]
        public void ATwoStoryBox_HasFloorsAndWalls_AndNoFrameProblem()
        {
            var frame = ShipFrameTestDesigns.TwoStoryBox();
            var shape = ShipFrameShape.From(frame);

            Assert.That(shape.Faces.Count(f => f.IsWall), Is.EqualTo(4));
            Assert.That(shape.Faces.Where(f => f.IsWall).Select(f => f.Story), Is.All.EqualTo(0), "a wall belongs to the story it rises from");
            Assert.That(shape.PlankFloors.Count(), Is.EqualTo(2));
            var validation = Validate(frame);
            Assert.That(HasFrameProblem(validation), Is.False, validation.ToString());
        }

        [Test]
        public void TooLittleHeadroom_UnderAFloor_IsReported()
        {
            var validation = Validate(ShipFrameTestDesigns.TwoStoryBox(storyHeight: 20));

            Assert.That(validation.Problems.Any(p => p.Kind == ShipDesignProblemKind.BadFace && p.InstanceId == 22 && p.Message.Contains("leaves 1.85 m")),
                Is.True, validation.ToString());
        }

        [Test]
        public void Railings_RunOnlyAlongFloorEdges_NotUnderAPlankedWall()
        {
            var frame = ShipFrameTestDesigns.TwoStoryBox();
            var railed = frame.With(edges: frame.Edges.Select(e => e.Id is 13 or 17 or 9 ? new ShipFrameEdge(e.Id, e.A, e.B, true) : e));

            var problems = Validate(railed).Problems.Where(p => p.Kind == ShipDesignProblemKind.BadEdge).ToList();

            Assert.That(problems.Single(p => p.InstanceId == 17).Message, Does.Contain("post"));
            Assert.That(problems.Single(p => p.InstanceId == 9).Message, Does.Contain("under the planked wall"));
            Assert.That(problems.Any(p => p.InstanceId == 13), Is.False, "the top deck's edge has no wall above it");
        }

        [Test]
        public void Walls_AreSlabsInTheirOwnPlane_BuiltWithTheLowerStory_AndRailingsAddPostsAndRails()
        {
            var frame = ShipFrameTestDesigns.TwoStoryBox();
            var processor = new ShipFrameMeshProcessor();
            var tuning = ShipFrameTuning.Default;

            var stories = processor.Build(frame, tuning);
            var railed = processor.Build(frame.With(edges: frame.Edges.Select(e => e.Id == 13 ? new ShipFrameEdge(13, e.A, e.B, true) : e)), tuning);

            // Story 0: its floor slab (24 corners) and four wall slabs (24 each); story 1: its floor only.
            Assert.That(stories[0].Planks.Vertices, Has.Count.EqualTo(24 * 5));
            Assert.That(stories[1].Planks.Vertices, Has.Count.EqualTo(24));
            float wallTop = stories[0].Planks.Vertices.Max(v => v.y);
            Assert.That(wallTop, Is.EqualTo(2.5f).Within(1e-3f), "walls reach the upper floor");
            Assert.That(railed[1].Beams.Vertices.Count, Is.GreaterThan(stories[1].Beams.Vertices.Count));
            Assert.That(railed[1].Beams.Vertices.Max(v => v.y), Is.EqualTo(2.5f + tuning.RailHeight + 0.032f).Within(0.01f), "the top rail");
            Assert.That(railed[0].Fingerprint, Is.EqualTo(stories[0].Fingerprint), "a railing upstairs leaves story 0 alone");
        }

        [Test]
        public void TheShipyardTools_JoinStoriesWithPosts_AndCloseWalls()
        {
            var tools = new ShipyardFrameToolProcessor();
            var limits = ShipDesignLimits.Default;
            var frame = ShipFrameTestDesigns.DeckFrame();
            var design = ShipFrameTestDesigns.FrameOnly(frame);
            var upstairs = new ShipyardFrameToolSettings(1, 5, -5, 30, limits);
            ShipyardFramePick Plane(float x, float z) => new(ShipyardFramePickKind.None, 0, Vector3.zero, true, new Vector3(x, 2.5f, z));
            ShipyardFramePick Node(int id) => new(ShipyardFramePickKind.Node, id, Vector3.zero, true, Vector3.zero);

            // A node on story 1 starts that story; joining it to corner 1 below is a post.
            var first = tools.Click(design, Plane(-2f, -2f), null, upstairs);
            Assert.That(first.Design!.Frame.StoryHeights.Count, Is.EqualTo(2));
            int top1 = first.Selected!.Value;
            var post1 = tools.Click(first.Design, Node(1), top1, upstairs);

            // A second post at corner 2, then the edge between their tops closes the wall 1-2-top2-top1.
            var second = tools.Click(post1.Design!, Plane(-2f, 2f), null, upstairs);
            int top2 = second.Selected!.Value;
            var post2 = tools.Click(second.Design!, Node(2), top2, upstairs);
            var joined = tools.Click(post2.Design!, Node(top1), top2, upstairs);

            var wall = joined.Design!.Frame.Faces.Single(f => f.Id != 9);
            Assert.That(wall.Nodes, Is.EquivalentTo(new[] { 1, 2, top1, top2 }));
            Assert.That(ShipFrameShape.From(joined.Design.Frame).Faces.Single(f => f.Face.Id == wall.Id).IsWall, Is.True);
        }
    }
}
