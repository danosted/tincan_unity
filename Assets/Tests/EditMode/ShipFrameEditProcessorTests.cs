#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipFrameEditProcessor"/>: frame edits keep faces in step (ids and fills survive), ids come from the shared
    /// counter and are never reused, and every edit is undone by restoring the frame before it.
    /// </summary>
    public class ShipFrameEditProcessorTests
    {
        private readonly ShipFrameEditProcessor _processor = new();
        private readonly ShipDesignLimits _limits = ShipDesignLimits.Default;

        private ShipDesign Apply(ShipDesign design, ShipFrameEdit edit)
        {
            var result = _processor.Apply(design, edit, _limits);
            Assert.That(result.Applied, Is.True, result.Refusal);
            return result.Design;
        }

        private string? Refusal(ShipDesign design, ShipFrameEdit edit) => _processor.Apply(design, edit, _limits).Refusal;

        /// <summary>Two 4 x 4 m squares side by side sharing edge 13 (3-4): faces 21 [1,2,3,4] and 22 [4,3,5,6].</summary>
        private static ShipDesign TwoSquares() => ShipFrameTestDesigns.FrameOnly(new ShipFrame(0, new[] { 30 },
            new[]
            {
                new ShipFrameNode(1, 0, -20, 0, -20), new ShipFrameNode(2, 0, -20, 0, 20), new ShipFrameNode(3, 0, 20, 0, 20),
                new ShipFrameNode(4, 0, 20, 0, -20), new ShipFrameNode(5, 0, 60, 0, 20), new ShipFrameNode(6, 0, 60, 0, -20),
            },
            new[]
            {
                new ShipFrameEdge(11, 1, 2), new ShipFrameEdge(12, 2, 3), new ShipFrameEdge(13, 3, 4), new ShipFrameEdge(14, 4, 1),
                new ShipFrameEdge(15, 3, 5), new ShipFrameEdge(16, 5, 6), new ShipFrameEdge(17, 6, 4),
            },
            new[] { new ShipFrameFace(21, new[] { 1, 2, 3, 4 }, ShipFrameFill.Planks), new ShipFrameFace(22, new[] { 4, 3, 5, 6 }, ShipFrameFill.Open) }));

        [Test]
        public void ADeck_IsBuiltEdgeByEdge_WithFreshIdsFromTheSharedCounter()
        {
            var design = ShipDesign.Empty("New");
            design = Apply(design, ShipFrameEdit.AddStory(30));
            var corners = new[] { (-20, -20), (-20, 20), (20, 20), (20, -20) };
            var nodes = corners.Select(c =>
            {
                var result = _processor.Apply(design, ShipFrameEdit.AddNode(0, c.Item1, 0, c.Item2), _limits);
                design = result.Design;
                return result.AddedInstanceId;
            }).ToList();
            for (int i = 0; i < 4; i++) design = Apply(design, ShipFrameEdit.AddEdge(nodes[i], nodes[(i + 1) % 4]));

            var face = _processor.Apply(design, ShipFrameEdit.AddFace(nodes, ShipFrameFill.Planks), _limits);

            Assert.That(nodes, Is.EqualTo(new[] { 1, 2, 3, 4 }));
            Assert.That(face.AddedInstanceId, Is.EqualTo(9));
            Assert.That(face.Design.NextPartInstanceId, Is.EqualTo(10));
            Assert.That(face.Design.Frame.ToString(), Is.EqualTo("frame (1 stories, 4 nodes, 4 edges, 1 faces)"));
        }

        [Test]
        public void NewIds_SkipPartIds_AndNewPartsSkipFrameIds()
        {
            var design = ShipFrameTestDesigns.Deck();
            using var parts = new ShipDesignTestParts();

            var node = _processor.Apply(design, ShipFrameEdit.AddNode(0, 0, 0, 40), _limits);
            var part = new ShipDesignEditProcessor().Apply(node.Design, ShipDesignEdit.Place(ShipDesignTestParts.Block, ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, 10, 10), 0),
                parts.Catalog, _limits);

            Assert.That(node.AddedInstanceId, Is.EqualTo(ShipFrameTestDesigns.NextId));
            Assert.That(part.AddedInstanceId, Is.EqualTo(ShipFrameTestDesigns.NextId + 1));
        }

        [Test]
        public void Undo_RestoresTheFrame_ButNeverWindsTheCounterBack()
        {
            var design = ShipFrameTestDesigns.Deck();
            var codec = new ShipDesignJsonCodec(_limits);

            var edited = _processor.Apply(design, ShipFrameEdit.MergeNodes(1, 2), _limits);
            var undone = new ShipDesignEditProcessor().Apply(edited.Design, edited.Undo!, new ShipPartCatalog(System.Array.Empty<TinCan.Core.Ship.Parts.ShipPartDefinition>()), _limits);
            var added = _processor.Apply(design, ShipFrameEdit.AddNode(0, 0, 0, 40), _limits);
            var addUndone = _processor.Apply(added.Design, added.Undo!.FrameEdit!, _limits);

            Assert.That(codec.Encode(undone.Design), Is.EqualTo(codec.Encode(design)));
            Assert.That(addUndone.Design.Frame.Nodes.Count, Is.EqualTo(4));
            Assert.That(addUndone.Design.NextPartInstanceId, Is.EqualTo(ShipFrameTestDesigns.NextId + 1), "id 11 was handed out; it is not handed out again");
        }

        [Test]
        public void Edges_JoinTwoNodesOnceOnTheSameOrAdjacentStories()
        {
            var design = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.AddStory(30));
            design = Apply(design, ShipFrameEdit.AddStory(30));
            var high = _processor.Apply(design, ShipFrameEdit.AddNode(2, 0, 0, 0), _limits);
            var middle = _processor.Apply(high.Design, ShipFrameEdit.AddNode(1, 0, 0, 0), _limits);

            Assert.That(Refusal(design, ShipFrameEdit.AddEdge(1, 1)), Does.Contain("two different"));
            Assert.That(Refusal(design, ShipFrameEdit.AddEdge(1, 99)), Does.Contain("no node #99"));
            Assert.That(Refusal(design, ShipFrameEdit.AddEdge(2, 1)), Does.Contain("already joined"));
            Assert.That(Refusal(middle.Design, ShipFrameEdit.AddEdge(1, high.AddedInstanceId)), Does.Contain("adjacent"));
            Assert.That(_processor.Apply(middle.Design, ShipFrameEdit.AddEdge(1, middle.AddedInstanceId), _limits).Applied, Is.True, "a post");
        }

        [Test]
        public void Faces_NeedAnEdgeOnEverySide_AndDifferentCorners()
        {
            var design = ShipFrameTestDesigns.Deck();

            Assert.That(Refusal(design, ShipFrameEdit.AddFace(new[] { 1, 2 }, ShipFrameFill.Planks)), Does.Contain("three"));
            Assert.That(Refusal(design, ShipFrameEdit.AddFace(new[] { 1, 2, 3 }, ShipFrameFill.Planks)), Does.Contain("#3 and #1"));
            Assert.That(Refusal(design, ShipFrameEdit.AddFace(new[] { 4, 3, 2, 1 }, ShipFrameFill.Open)), Does.Contain("already has these corners"));
        }

        [Test]
        public void MergingNodes_MovesTheirEdgesAndCorners_AndAFaceKeepsItsId()
        {
            var merged = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.MergeNodes(1, 2)).Frame;

            Assert.That(merged.Nodes.Select(n => n.Id), Is.EqualTo(new[] { 1, 3, 4 }));
            Assert.That(merged.Edges.Select(e => (e.Id, e.A, e.B)), Is.EqualTo(new[] { (6, 1, 3), (7, 3, 4), (8, 4, 1) }), "the edge 1-2 folded away");
            Assert.That(merged.Edges.Single(e => e.Id == 6).Railing, Is.True);
            Assert.That(merged.Faces.Single().Id, Is.EqualTo(9));
            Assert.That(merged.Faces.Single().Nodes, Is.EqualTo(new[] { 1, 3, 4 }));
        }

        [Test]
        public void MergingNodes_CollapsesDoubledEdges_IntoTheLowerId_KeepingARailing()
        {
            var design = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.SetRailing(5, ShipFrameRailing.None));
            design = Apply(design, ShipFrameEdit.SetRailing(6, ShipFrameRailing.Rail));

            // Merging 3 into 1 (diagonal corners): edge 6 (2-3, railing) becomes a second 1-2 beside edge 5, and edge 7
            // becomes 1-4, which edge 8 then doubles.
            var frame = Apply(design, ShipFrameEdit.MergeNodes(1, 3)).Frame;

            Assert.That(frame.Edges.Select(e => (e.Id, e.A, e.B, e.Railing)), Is.EqualTo(new[] { (5, 1, 2, true), (7, 1, 4, false) }));
            Assert.That(frame.Faces, Is.Empty, "the face is pinched at node 1");
        }

        [Test]
        public void MergingNodes_OnDifferentStories_IsRefused()
        {
            var design = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.AddStory(30));
            var upper = _processor.Apply(design, ShipFrameEdit.AddNode(1, 0, 0, 0), _limits);

            Assert.That(Refusal(upper.Design, ShipFrameEdit.MergeNodes(1, upper.AddedInstanceId)), Does.Contain("same story"));
        }

        [Test]
        public void RemovingAnEdge_TakesTheFaceItBounds()
        {
            var frame = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.RemoveEdge(5)).Frame;

            Assert.That(frame.Edges.Select(e => e.Id), Is.EqualTo(new[] { 6, 7, 8 }));
            Assert.That(frame.Faces, Is.Empty);
        }

        [Test]
        public void RemovingAnEdgeBetweenTwoFaces_MergesThemIntoTheLowerId()
        {
            var frame = Apply(TwoSquares(), ShipFrameEdit.RemoveEdge(13)).Frame;

            var face = frame.Faces.Single();
            Assert.That((face.Id, face.Fill), Is.EqualTo((21, ShipFrameFill.Planks)));
            Assert.That(face.Nodes, Is.EquivalentTo(new[] { 1, 2, 3, 5, 6, 4 }));
            for (int i = 0; i < face.Nodes.Count; i++)
            {
                Assert.That(frame.TryGetEdgeBetween(face.Nodes[i], face.Nodes[(i + 1) % face.Nodes.Count], out _), Is.True, $"side {i} of {face}");
            }

            Assert.That(ShipFrameGeometry.IsSimpleXZ(face.Nodes.Select(frame.Position).ToList()), Is.True);
        }

        [Test]
        public void SplittingAnEdge_AddsTheCornerToTheFacesItBounds()
        {
            var design = TwoSquares();

            var split = _processor.Apply(design, ShipFrameEdit.SplitEdge(13, 20, 0, 0), _limits);

            int node = split.AddedInstanceId;
            var frame = split.Design.Frame;
            Assert.That(node, Is.EqualTo(23));
            Assert.That(frame.Edges.Single(e => e.Id == 13).Joins(3, node), Is.True);
            Assert.That(frame.Edges.Single(e => e.Id == 24).Joins(node, 4), Is.True);
            Assert.That(frame.Faces.Single(f => f.Id == 21).Nodes, Is.EqualTo(new[] { 1, 2, 3, node, 4 }));
            Assert.That(frame.Faces.Single(f => f.Id == 22).Nodes, Is.EqualTo(new[] { 4, node, 3, 5, 6 }));
            Assert.That(split.Design.NextPartInstanceId, Is.EqualTo(25));
        }

        [Test]
        public void SplittingAPost_IsRefused()
        {
            var design = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.AddStory(30));
            var upper = _processor.Apply(design, ShipFrameEdit.AddNode(1, -20, 0, -20), _limits);
            var post = _processor.Apply(upper.Design, ShipFrameEdit.AddEdge(1, upper.AddedInstanceId), _limits);

            Assert.That(Refusal(post.Design, ShipFrameEdit.SplitEdge(post.AddedInstanceId, -20, 15, -20)), Does.Contain("Only floor edges"));
        }

        [Test]
        public void SplittingAFace_KeepsTheIdOnTheLargerHalf()
        {
            // A pentagon: a 4 x 4 m square with a point on its +X side. Cut from corner 1 to corner 3: the triangle 1-2-3 is
            // 8 m², the rest 12 m².
            var design = ShipFrameTestDesigns.FrameOnly(new ShipFrame(0, new[] { 30 },
                new[]
                {
                    new ShipFrameNode(1, 0, 0, 0, 0), new ShipFrameNode(2, 0, 0, 0, 40), new ShipFrameNode(3, 0, 40, 0, 40),
                    new ShipFrameNode(4, 0, 60, 0, 20), new ShipFrameNode(5, 0, 40, 0, 0),
                },
                new[] { new ShipFrameEdge(6, 1, 2), new ShipFrameEdge(7, 2, 3), new ShipFrameEdge(8, 3, 4), new ShipFrameEdge(9, 4, 5), new ShipFrameEdge(10, 5, 1) },
                new[] { new ShipFrameFace(11, new[] { 1, 2, 3, 4, 5 }, ShipFrameFill.Planks) }));

            var split = _processor.Apply(design, ShipFrameEdit.SplitFace(11, 1, 3), _limits);

            Assert.That(split.Applied, Is.True, split.Refusal);
            var frame = split.Design.Frame;
            Assert.That(frame.Faces.Single(f => f.Id == 11).Nodes, Is.EqualTo(new[] { 3, 4, 5, 1 }));
            var added = frame.Faces.Single(f => f.Id == split.AddedInstanceId);
            Assert.That(added.Fill, Is.EqualTo(ShipFrameFill.Planks));
            Assert.That(added.Nodes, Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(frame.TryGetEdgeBetween(1, 3, out _), Is.True, "the cut is an edge");
            Assert.That(Refusal(design, ShipFrameEdit.SplitFace(11, 1, 2)), Does.Contain("neighbours"));
        }

        [Test]
        public void RemovingANode_TakesItsEdgesAndFaces()
        {
            var frame = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.RemoveNode(3)).Frame;

            Assert.That(frame.Nodes.Select(n => n.Id), Is.EqualTo(new[] { 1, 2, 4 }));
            Assert.That(frame.Edges.Select(e => e.Id), Is.EqualTo(new[] { 5, 8 }));
            Assert.That(frame.Faces, Is.Empty);
        }

        [Test]
        public void MovingNodes_FillsAndRailings_ChangeOnlyWhatTheySay()
        {
            var design = Apply(ShipFrameTestDesigns.Deck(), ShipFrameEdit.MoveNode(3, 30, -3, 25));
            design = Apply(design, ShipFrameEdit.SetFaceFill(9, ShipFrameFill.Open));
            design = Apply(design, ShipFrameEdit.SetRailing(7, ShipFrameRailing.Rail));

            var frame = design.Frame;
            frame.TryGetNode(3, out var moved);
            Assert.That((moved.X, moved.Y, moved.Z, moved.Story), Is.EqualTo((30, -3, 25, 0)));
            Assert.That(frame.Faces.Single().Fill, Is.EqualTo(ShipFrameFill.Open));
            Assert.That(frame.Edges.Where(e => e.Railing).Select(e => e.Id), Is.EqualTo(new[] { 6, 7 }));
        }

        [Test]
        public void Limits_BoundThePositions_TheStories_AndTheElementCount()
        {
            var design = ShipFrameTestDesigns.Deck();
            var limits = ShipDesignLimits.Default;
            limits.MaxFrameElements = 9;
            limits.MaxStories = 1;

            Assert.That(Refusal(design, ShipFrameEdit.MoveNode(3, 129 * ShipFrameUnits.PerMetre, 0, 0)), Does.Contain("from the origin"));
            Assert.That(_processor.Apply(design, ShipFrameEdit.AddNode(0, 0, 0, 0), limits).Refusal, Does.Contain("full"));
            Assert.That(_processor.Apply(design, ShipFrameEdit.AddStory(30), limits).Refusal, Does.Contain("stories already"));
            Assert.That(Refusal(design, ShipFrameEdit.AddNode(3, 0, 0, 0)), Does.Contain("no story 3"));
        }

        [Test]
        public void Stairs_HangFromAFloorEdgeAboveTheLowestStory_FlipAndGo_AndGoWithTheirEdge()
        {
            var well = ShipFrameTestDesigns.FrameOnly(ShipFrameTestDesigns.StairWell().With(stairs: System.Array.Empty<ShipFrameStair>()));

            var added = _processor.Apply(well, ShipFrameEdit.AddStair(15, -1), _limits);
            Assert.That(added.Applied, Is.True, added.Refusal);
            int id = added.AddedInstanceId;
            Assert.That(added.Design.Frame.Stairs.Single().Edge, Is.EqualTo(15));

            Assert.That(Refusal(added.Design, ShipFrameEdit.AddStair(15, 1)), Does.Contain("already"));
            Assert.That(Refusal(well, ShipFrameEdit.AddStair(9, 1)), Does.Contain("story below"), "edge 9 is on story 0");
            Assert.That(Refusal(well, ShipFrameEdit.AddStair(17, 1)), Does.Contain("post"));

            var flipped = Apply(added.Design, ShipFrameEdit.FlipStair(id));
            Assert.That(flipped.Frame.Stairs.Single().Dir, Is.EqualTo(1));
            Assert.That(Apply(flipped, ShipFrameEdit.RemoveStair(id)).Frame.Stairs, Is.Empty);
            Assert.That(Apply(added.Design, ShipFrameEdit.RemoveNode(7)).Frame.Stairs, Is.Empty, "its edge went with the node");

            var undone = Apply(added.Design, added.Undo!.FrameEdit!);
            Assert.That(undone.Frame.Stairs, Is.Empty, "one undo");
        }
    }
}
