#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="ShipFrameValidator"/> through <see cref="ShipDesignValidator"/>: the frame's own rules, ids shared with
    /// parts, the frame's weight, and parts and frame joined to the core through each other.
    /// </summary>
    public class ShipFrameValidatorTests
    {
        private ShipDesignTestParts _parts = null!;
        private readonly ShipDesignValidator _validator = new();

        [SetUp]
        public void SetUp() => _parts = new ShipDesignTestParts();

        [TearDown]
        public void TearDown() => _parts.Dispose();

        private ShipDesignValidation Validate(ShipDesign design, ShipFrameTuning? tuning = null) =>
            _validator.Validate(design, _parts.Catalog, ShipDesignLimits.Default, tuning);

        private static ShipDesign WithFrame(ShipDesign design, ShipFrame frame) =>
            design.WithFrame(frame, System.Math.Max(design.NextPartInstanceId, frame.Ids.DefaultIfEmpty(0).Max() + 1));

        private static ShipDesign WithPart(ShipDesign design, int id, string part, int x, int y, int z) =>
            design.WithParts(design.Parts.Append(new ShipPartPlacement(id, part, new ShipGridCell(x, y, z), 0)), System.Math.Max(design.NextPartInstanceId, id + 1));

        [Test]
        public void TheHelmOnTheTestDeck_IsValid()
        {
            var validation = Validate(ShipFrameTestDesigns.Deck());

            Assert.That(validation.Problems, Is.Empty, validation.ToString());
        }

        [Test]
        public void ANodeDriftingOutOfItsBand_IsReported()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var drifted = frame.With(nodes: frame.Nodes.Select(n => n.Id == 3 ? n.MovedTo(n.X, 9, n.Z) : n));

            var validation = Validate(WithFrame(ShipFrameTestDesigns.Deck(), drifted));

            Assert.That(validation.Problems.Single(p => p.Kind == ShipDesignProblemKind.DriftOutOfBand).InstanceId, Is.EqualTo(3));
        }

        [Test]
        public void APlankFloorTooSteepToWalk_IsReported_ButAnOpenFaceMaySlope()
        {
            var steep = ShipFrameTuning.Default;
            steep.MaxFloorSlope = 1f;

            Assert.That(Validate(ShipFrameTestDesigns.Deck(), steep).Has(ShipDesignProblemKind.FloorTooSteep), Is.True);
            Assert.That(Validate(ShipFrameTestDesigns.Deck(ShipFrameFill.Open), steep).Has(ShipDesignProblemKind.FloorTooSteep), Is.False);
        }

        [Test]
        public void ANodeWithOneEdge_Dangles()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var spur = frame.With(nodes: frame.Nodes.Append(new ShipFrameNode(11, 0, -40, 0, -20)), edges: frame.Edges.Append(new ShipFrameEdge(12, 1, 11)));

            var validation = Validate(WithFrame(ShipFrameTestDesigns.Deck(), spur));

            Assert.That(validation.Problems.Single(p => p.Kind == ShipDesignProblemKind.DanglingNode).InstanceId, Is.EqualTo(11));
        }

        [Test]
        public void AFaceThatCrossesItself_IsBad()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var bowtie = frame.With(
                edges: new[] { new ShipFrameEdge(5, 1, 3), new ShipFrameEdge(6, 3, 2), new ShipFrameEdge(7, 2, 4), new ShipFrameEdge(8, 4, 1) },
                faces: new[] { new ShipFrameFace(9, new[] { 1, 3, 2, 4 }, ShipFrameFill.Planks) });

            var validation = Validate(WithFrame(ShipFrameTestDesigns.Deck(), bowtie));

            Assert.That(validation.Problems.Any(p => p.Kind == ShipDesignProblemKind.BadFace && p.Message.Contains("crosses itself")), Is.True, validation.ToString());
        }

        [Test]
        public void PlankFacesOverlapping_AreBad_ButSharingASideIsFine()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var second = frame.With(
                nodes: frame.Nodes.Concat(new[] { new ShipFrameNode(11, 0, 0, 0, 0), new ShipFrameNode(12, 0, 0, 0, 40), new ShipFrameNode(13, 0, 40, 0, 40), new ShipFrameNode(14, 0, 40, 0, 0) }),
                edges: frame.Edges.Concat(new[] { new ShipFrameEdge(15, 11, 12), new ShipFrameEdge(16, 12, 13), new ShipFrameEdge(17, 13, 14), new ShipFrameEdge(18, 14, 11) }),
                faces: frame.Faces.Append(new ShipFrameFace(19, new[] { 11, 12, 13, 14 }, ShipFrameFill.Planks)));
            var beside = frame.With(
                nodes: frame.Nodes.Concat(new[] { new ShipFrameNode(11, 0, 60, 0, 20), new ShipFrameNode(12, 0, 60, 0, -20) }),
                edges: frame.Edges.Concat(new[] { new ShipFrameEdge(15, 3, 11), new ShipFrameEdge(16, 11, 12), new ShipFrameEdge(17, 12, 4) }),
                faces: frame.Faces.Append(new ShipFrameFace(19, new[] { 4, 3, 11, 12 }, ShipFrameFill.Planks)));

            var overlapping = Validate(WithFrame(ShipFrameTestDesigns.Deck(), second));
            var sharing = Validate(WithFrame(ShipFrameTestDesigns.Deck(), beside));

            Assert.That(overlapping.Problems.Any(p => p.Kind == ShipDesignProblemKind.BadFace && p.Message.Contains("overlaps")), Is.True, overlapping.ToString());
            Assert.That(sharing.Has(ShipDesignProblemKind.BadFace), Is.False, sharing.ToString());
        }

        [Test]
        public void AFaceAcrossThreeStories_AndReferencesToMissingNodes_AreReported()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var threeStories = frame.With(
                storyHeights: new[] { 30, 30, 30 },
                nodes: frame.Nodes.Select(n => n.Id == 3 ? new ShipFrameNode(3, 2, n.X, 0, n.Z) : n),
                edges: frame.Edges.Append(new ShipFrameEdge(12, 1, 99)));

            var validation = Validate(WithFrame(ShipFrameTestDesigns.Deck(), threeStories));

            Assert.That(validation.Problems.Any(p => p.Kind == ShipDesignProblemKind.BadFace && p.Message.Contains("more than two stories")), Is.True, validation.ToString());
            Assert.That(validation.Problems.Any(p => p.Kind == ShipDesignProblemKind.FrameReference && p.InstanceId == 12), Is.True, validation.ToString());
        }

        [Test]
        public void PartsAndFrameElements_ShareOneIdSpace()
        {
            var design = WithPart(ShipFrameTestDesigns.Deck(), 3, ShipDesignTestParts.Block, 5, 0, 5);

            Assert.That(Validate(design).Problems.Any(p => p.Kind == ShipDesignProblemKind.DuplicateInstanceId && p.InstanceId == 3), Is.True);
        }

        [Test]
        public void AHelmNotStandingOnTheDeck_LeavesTheFrameDisconnected()
        {
            var design = ShipFrameTestDesigns.Deck();
            var off = design.WithParts(new[] { new ShipPartPlacement(10, ShipDesignTestParts.Helm, new ShipGridCell(10, 0, 0), 0) }, design.NextPartInstanceId);

            var validation = Validate(off);

            Assert.That(validation.Problems.Single(p => p.Kind == ShipDesignProblemKind.Disconnected).InstanceId, Is.EqualTo(1), "one problem for the whole deck, named by its lowest node");
        }

        [Test]
        public void PartsJoin_ThroughTheDeck_AndAPartFarFromItIsDisconnected()
        {
            // The helm stands on one side of the deck, a block on the other: they touch only through the planks.
            var design = WithPart(ShipFrameTestDesigns.Deck(), 11, ShipDesignTestParts.Block, 1, 0, 1);
            design = WithPart(design, 12, ShipDesignTestParts.Block, 1, 5, 1);

            var validation = Validate(design);

            Assert.That(validation.Problems.Where(p => p.Kind == ShipDesignProblemKind.Disconnected).Select(p => p.InstanceId), Is.EqualTo(new[] { 12 }));
        }

        [Test]
        public void AFramePartTouchesABeam_EvenOffThePlanks()
        {
            // An open deck: nothing to stand on, but a block in the middle of the -X beam joins it.
            var design = WithPart(ShipFrameTestDesigns.Deck(ShipFrameFill.Open), 11, ShipDesignTestParts.Block, -2, -1, 0);

            var validation = Validate(design);

            Assert.That(validation.Problems.Where(p => p.Kind == ShipDesignProblemKind.Disconnected).Select(p => p.InstanceId), Is.EqualTo(new[] { 11, 1 }),
                "the helm stands on nothing; the block and the frame hang together, apart from the helm");
            var joined = WithPart(design, 12, ShipDesignTestParts.Block, -1, -1, 0);
            joined = WithPart(joined, 13, ShipDesignTestParts.Block, 0, -1, 0);
            Assert.That(Validate(joined).Has(ShipDesignProblemKind.Disconnected), Is.False, "blocks under the helm reach the beam");
        }

        [Test]
        public void TheFrameWeighs()
        {
            var frame = ShipFrameTestDesigns.DeckFrame();
            var huge = frame.With(nodes: frame.Nodes.Select(n => n.MovedTo(n.X * 10, 0, n.Z * 10)));

            Assert.That(Validate(WithFrame(ShipFrameTestDesigns.Deck(), huge)).Has(ShipDesignProblemKind.TooHeavy), Is.True, "40 x 40 m of planks outweighs the helm's lift");
        }
    }
}
