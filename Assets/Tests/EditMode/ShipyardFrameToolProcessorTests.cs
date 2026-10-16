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
    /// <see cref="ShipyardFrameToolProcessor"/>: clicks add, select, join and split; a loop closed by a new edge becomes a
    /// planked face; drags move and merge; fill, drift and delete; each gesture undone in one step.
    /// </summary>
    public class ShipyardFrameToolProcessorTests
    {
        private readonly ShipyardFrameToolProcessor _tools = new();
        private readonly ShipFrameEditProcessor _edits = new();
        private static readonly ShipyardFrameToolSettings Settings = new(0, 5, -5, 30, ShipDesignLimits.Default);

        private static ShipyardFramePick OnPlane(float x, float z) => new(ShipyardFramePickKind.None, 0, Vector3.zero, true, new Vector3(x, -0.5f, z));

        private static ShipyardFramePick On(ShipyardFramePickKind kind, int id, Vector3 point = default) => new(kind, id, point, true, point);

        private ShipDesign Undo(ShipyardFrameToolResult result) => _edits.Apply(result.Design!, result.Undo!.FrameEdit!, ShipDesignLimits.Default).Design;

        [Test]
        public void TheFirstClick_OnAnEmptyDesign_MakesTheFirstStory_AndANodeSnappedToHalfAMetre()
        {
            var result = _tools.Click(ShipDesign.Empty("New"), OnPlane(1.26f, -0.74f), null, Settings);

            var frame = result.Design!.Frame;
            Assert.That((frame.Base, frame.StoryHeights.Single()), Is.EqualTo((-5, 30)));
            Assert.That(frame.Nodes.Single().X, Is.EqualTo(15));
            Assert.That(frame.Nodes.Single().Z, Is.EqualTo(-5));
            Assert.That(result.Selected, Is.EqualTo(frame.Nodes.Single().Id));
            Assert.That(Undo(result).Frame.IsEmpty, Is.True, "one undo takes the story and the node");
        }

        [Test]
        public void AClickWithANodeSelected_AddsAJoinedNode_AndClosingALoopPlanksIt()
        {
            var design = ShipDesign.Empty("New");
            int? selected = null;
            foreach (var (x, z) in new[] { (0f, 0f), (0f, 2f), (2f, 2f) })
            {
                var step = _tools.Click(design, OnPlane(x, z), selected, Settings);
                design = step.Design!;
                selected = step.Selected;
            }

            Assert.That(design.Frame.Edges.Count, Is.EqualTo(2));
            int first = design.Frame.Nodes[0].Id;

            var closed = _tools.Click(design, On(ShipyardFramePickKind.Node, first, design.Frame.Position(first)), selected, Settings);

            var face = closed.Design!.Frame.Faces.Single();
            Assert.That(face.Fill, Is.EqualTo(ShipFrameFill.Planks));
            Assert.That(face.Nodes, Is.EquivalentTo(design.Frame.Nodes.Select(n => n.Id)));
            Assert.That(closed.Selected, Is.EqualTo(first));
            Assert.That(Undo(closed).Frame.Faces, Is.Empty);
        }

        [Test]
        public void ClickingNodes_SelectsAndDeselects_AndJoiningTwoCornersOfAFace_SplitsIt()
        {
            var design = ShipFrameTestDesigns.Deck();

            var select = _tools.Click(design, On(ShipyardFramePickKind.Node, 1), null, Settings);
            var deselect = _tools.Click(design, On(ShipyardFramePickKind.Node, 1), 1, Settings);
            var split = _tools.Click(design, On(ShipyardFramePickKind.Node, 3), 1, Settings);

            Assert.That((select.Changed, select.Selected), Is.EqualTo((false, (int?)1)));
            Assert.That((deselect.Changed, deselect.Selected), Is.EqualTo((false, (int?)null)));
            Assert.That(split.Design!.Frame.Faces.Count, Is.EqualTo(2));
            Assert.That(split.Design.Frame.TryGetEdgeBetween(1, 3, out _), Is.True);
        }

        [Test]
        public void ClickingAnEdge_PutsANodeOnIt()
        {
            var design = ShipFrameTestDesigns.Deck();

            var result = _tools.Click(design, On(ShipyardFramePickKind.Edge, 5, new Vector3(-2f, -0.5f, 0.1f)), null, Settings);

            var frame = result.Design!.Frame;
            frame.TryGetNode(result.Selected!.Value, out var added);
            Assert.That((added.X, added.Y, added.Z), Is.EqualTo((-20, 0, 0)));
            Assert.That(frame.Faces.Single().Nodes, Has.Count.EqualTo(5));
        }

        [Test]
        public void Dragging_ShowsTheMove_DropMovesIt_OrMergesItIntoTheNodeUnderIt()
        {
            var design = ShipFrameTestDesigns.Deck();

            var preview = _tools.DragPreview(design, 1, new Vector3(-3f, -0.5f, -2f), Settings);
            var moved = _tools.Drop(design, 1, OnPlane(-3f, -2f), new Vector3(-3f, -0.5f, -2f), Settings);
            var merged = _tools.Drop(design, 1, On(ShipyardFramePickKind.Node, 2), new Vector3(-2f, -0.5f, 2f), Settings);
            var still = _tools.Drop(design, 1, OnPlane(-2.1f, -2f), new Vector3(-2.1f, -0.5f, -2f), Settings);

            preview!.Frame.TryGetNode(1, out var shown);
            Assert.That(shown.X, Is.EqualTo(-30));
            moved.Design!.Frame.TryGetNode(1, out var node);
            Assert.That(node.X, Is.EqualTo(-30));
            Assert.That(merged.Design!.Frame.TryGetNode(1, out _), Is.False);
            Assert.That(merged.Selected, Is.EqualTo(2));
            Assert.That(still.Changed, Is.False, "within the snap: no move");
        }

        [Test]
        public void SplittingTwoEdgesOfAFace_OneAfterTheOther_DividesTheFace()
        {
            var design = ShipFrameTestDesigns.Deck();

            // A node on the +Z side (edge 6, selected afterwards), then one on the -Z side (edge 8): they share face 9.
            var first = _tools.Click(design, On(ShipyardFramePickKind.Edge, 6, new Vector3(0f, -0.4f, 2f)), null, Settings);
            var second = _tools.Click(first.Design!, On(ShipyardFramePickKind.Edge, 8, new Vector3(0f, -0.5f, -2f)), first.Selected, Settings);

            var frame = second.Design!.Frame;
            Assert.That(frame.Faces, Has.Count.EqualTo(2), "the deck is split in two");
            Assert.That(frame.Faces.Sum(f => f.Nodes.Count), Is.EqualTo(8), "two quads: each new corner on both");
            Assert.That(frame.TryGetEdgeBetween(first.Selected!.Value, second.Selected!.Value, out _), Is.True);
        }

        [Test]
        public void ExtrudingAWall_PushesItOut_WithFloorBelow_DeckAbove_WallsBeside_AndOpensTheOldOne()
        {
            var design = ShipFrameTestDesigns.FrameOnly(ShipFrameTestDesigns.TwoStoryBox());

            // Wall 23 is the -X side (corners 1, 2, 6, 5); 20 units is 2 m.
            var result = _tools.Extrude(design, On(ShipyardFramePickKind.Face, 23), null, 20, Settings);

            var frame = result.Design!.Frame;
            var added = frame.Nodes.Where(n => n.Id > 26).ToList();
            Assert.That(added.Select(n => n.X), Is.All.EqualTo(-40), "pushed out along -X");
            Assert.That(added.Count(n => n.Story == 0), Is.EqualTo(2));
            Assert.That(frame.Faces.Single(f => f.Id == 23).Fill, Is.EqualTo(ShipFrameFill.Open), "the old wall opens");
            var shape = ShipFrameShape.From(frame);
            var newFaces = shape.Faces.Where(f => f.Face.Id > 26).ToList();
            Assert.That((newFaces.Count(f => f.IsWall), newFaces.Count(f => !f.IsWall)), Is.EqualTo((3, 2)), "the moved wall and two side walls; floor and deck");
            Assert.That(Undo(result).Frame.Faces.Count, Is.EqualTo(6), "one undo");
        }

        [Test]
        public void ExtrudingAnOuterFloorEdge_GrowsTheDeck_AndMovesItsRailingOut()
        {
            var design = ShipFrameTestDesigns.Deck();

            // Edge 6 (2-3) is the railed +Z side.
            var result = _tools.Extrude(design, On(ShipyardFramePickKind.Edge, 6), null, 20, Settings);

            var frame = result.Design!.Frame;
            Assert.That(frame.Faces.Count, Is.EqualTo(2));
            Assert.That(frame.Nodes.Where(n => n.Id > 10).Select(n => n.Z), Is.All.EqualTo(40));
            Assert.That(frame.Edges.Single(e => e.Id == 6).Railing, Is.False);
            Assert.That(frame.Edges.Single(e => e.Railing).Joins(frame.Nodes[^2].Id, frame.Nodes[^1].Id), Is.True, "the railing is on the new outer edge");
            Assert.That(_tools.Extrude(result.Design, On(ShipyardFramePickKind.Edge, 6), null, 20, Settings).Changed, Is.False, "an inner edge does not extrude");
        }

        [Test]
        public void ExtrudingAFloor_RaisesAStoryOnIt()
        {
            var design = ShipFrameTestDesigns.Deck();

            var result = _tools.Extrude(design, On(ShipyardFramePickKind.Face, 9), null, 20, Settings);

            var frame = result.Design!.Frame;
            Assert.That(frame.StoryHeights.Count, Is.EqualTo(2));
            Assert.That(frame.Nodes.Count(n => n.Story == 1), Is.EqualTo(4));
            var shape = ShipFrameShape.From(frame);
            Assert.That((shape.Faces.Count(f => f.IsWall), shape.Faces.Count(f => !f.IsWall)), Is.EqualTo((4, 2)));
            Assert.That(frame.Faces.Where(f => f.Id != 9).Select(f => f.Fill), Is.All.EqualTo(ShipFrameFill.Open), "extruding makes open faces");
            Assert.That(new ShipDesignValidator().Validate(result.Design, new ShipPartCatalog(System.Array.Empty<TinCan.Core.Ship.Parts.ShipPartDefinition>()),
                ShipDesignLimits.Default).Problems.Where(p => p.Kind is ShipDesignProblemKind.BadFace or ShipDesignProblemKind.BadEdge), Is.Empty,
                "the deck's railing, now under a wall, was taken off");
        }

        [Test]
        public void Fill_Drift_AndDelete()
        {
            var design = ShipFrameTestDesigns.Deck();

            var opened = _tools.ToggleFill(design, On(ShipyardFramePickKind.Face, 9), 1, ShipDesignLimits.Default);
            var raised = _tools.Drift(design, 1, 2, ShipDesignLimits.Default);
            var deleted = _tools.Delete(design, On(ShipyardFramePickKind.Node, 1), 1, ShipDesignLimits.Default);
            var nothing = _tools.Drift(design, null, 1, ShipDesignLimits.Default);

            Assert.That(opened.Design!.Frame.Faces[0].Fill, Is.EqualTo(ShipFrameFill.Open));
            Assert.That(opened.Selected, Is.EqualTo(1), "the selection stays");
            raised.Design!.Frame.TryGetNode(1, out var node);
            Assert.That(node.Y, Is.EqualTo(2));
            Assert.That((deleted.Design!.Frame.TryGetNode(1, out _), deleted.Selected), Is.EqualTo((false, (int?)null)));
            Assert.That((nothing.Changed, nothing.Message), Is.EqualTo((false, "Select a node to raise or lower it.")));
        }
    }
}
