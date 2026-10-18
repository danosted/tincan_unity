#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipDesignValidator"/>: every rule a flyable design keeps, parts hung on the frame.</summary>
    public class ShipDesignValidatorTests
    {
        private ShipDesignTestParts _parts = null!;
        private readonly ShipDesignValidator _validator = new();

        [SetUp]
        public void SetUp() => _parts = new ShipDesignTestParts();

        [TearDown]
        public void TearDown() => _parts.Dispose();

        private ShipDesignValidation Validate(ShipDesign design, ShipDesignLimits? limits = null) =>
            _validator.Validate(design, _parts.Catalog, limits ?? ShipDesignLimits.Default);

        private static ShipDesign With(ShipDesign design, params ShipPartPlacement[] parts) =>
            design.WithParts(design.Parts.Concat(parts), System.Math.Max(design.NextPartInstanceId, parts.Max(p => p.InstanceId) + 1));

        [Test]
        public void AHelmOnTheFloor_IsValid()
        {
            var result = Validate(Design((Helm, 0, 0, 0), (Beam, 2, 0, 0)));

            Assert.That(result.IsValid, Is.True, result.ToString());
            Assert.That(result.Problems, Is.Empty);
        }

        [Test]
        public void ADesignWithoutAHelm_HasNoCore()
        {
            Assert.That(Validate(Design((Block, 0, 0, 0))).Has(ShipDesignProblemKind.MissingCore), Is.True);
            Assert.That(Validate(Design()).Has(ShipDesignProblemKind.MissingCore), Is.True, "an empty design cannot fly");
        }

        [Test]
        public void TwoHelms_AreOneTooMany_AndSoAreTwoCairns()
        {
            Assert.That(Validate(Design((Helm, 0, 0, 0), (Helm, 3, 0, 0))).Has(ShipDesignProblemKind.ExtraCore), Is.True);
            Assert.That(Validate(Design((Helm, 0, 0, 0), (Cairn, 3, 0, 0), (Cairn, 5, 0, 0))).Has(ShipDesignProblemKind.ExtraCairn), Is.True);
        }

        [Test]
        public void PartsWhoseBlocksMeet_Overlap_YawedFootprintsIncluded()
        {
            Assert.That(Validate(Design((Helm, 0, 0, 0), (Block, 0, 0, 0))).Has(ShipDesignProblemKind.Overlap), Is.True, "the same spot");
            // A beam turned once runs along -Z: blocks at z 0, -1, -2.
            Assert.That(Validate(Design((Helm, 3, 0, 0), (Beam, 0, 0, 1), (Block, 0, -2, 0))).Has(ShipDesignProblemKind.Overlap), Is.True);
            Assert.That(Validate(Design((Helm, 3, 0, 0), (Beam, 0, 0, 1), (Block, 1, -2, 0))).Has(ShipDesignProblemKind.Overlap), Is.False, "side by side");
        }

        [Test]
        public void APart_IsHeldByItsAnchor_OrReportedUnanchored()
        {
            var design = Design((Helm, 0, 0, 0));

            var offTheFloor = With(design, new ShipPartPlacement(20, Block, ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, 300, 0), 0));
            var noSuchFace = With(design, new ShipPartPlacement(20, Block, ShipPartAnchor.OnFace(77, 0, 0), 0));
            var faceOnANode = With(design, new ShipPartPlacement(20, Block, ShipPartAnchor.OnNode(1), 0));
            var postOnANode = With(design, new ShipPartPlacement(20, Post, ShipPartAnchor.OnNode(1), 0));

            Assert.That(Validate(offTheFloor).Has(ShipDesignProblemKind.UnanchoredPart), Is.True, "30 m out, off the 20 m floor");
            Assert.That(Validate(noSuchFace).Has(ShipDesignProblemKind.UnanchoredPart), Is.True);
            Assert.That(Validate(faceOnANode).Has(ShipDesignProblemKind.UnanchoredPart), Is.True, "a block stands on a floor");
            Assert.That(Validate(postOnANode).Problems, Is.Empty, "a post sits on a node");
        }

        [Test]
        public void APartOnAPieceOfFrameApartFromTheHelms_IsDisconnected()
        {
            // A second floor beside the first, joined to nothing: a block on it is not on the ship.
            var floor = ShipFrameTestDesigns.Floor();
            var island = floor.With(
                nodes: floor.Nodes.Concat(new[] { new ShipFrameNode(30, 0, 300, 0, 0), new ShipFrameNode(31, 0, 300, 0, 30), new ShipFrameNode(32, 0, 330, 0, 30) }),
                edges: floor.Edges.Concat(new[] { new ShipFrameEdge(33, 30, 31), new ShipFrameEdge(34, 31, 32), new ShipFrameEdge(35, 32, 30) }),
                faces: floor.Faces.Append(new ShipFrameFace(36, new[] { 30, 31, 32 }, ShipFrameFill.Planks)));
            var design = new ShipDesign("Islands", "Tests", 40, new[]
            {
                new ShipPartPlacement(37, Helm, ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, 0, 0), 0),
                new ShipPartPlacement(38, Block, ShipPartAnchor.OnFace(36, 320, 20), 0),
            }, frame: island);

            var result = Validate(design);

            Assert.That(result.Problems.Where(p => p.Kind == ShipDesignProblemKind.Disconnected).Select(p => p.InstanceId), Is.EquivalentTo(new[] { 38, 30 }),
                "the block, and the loose piece named by its lowest node");
        }

        [Test]
        public void AnUnknownPart_IsReported_ButDoesNotBlock()
        {
            var result = Validate(Design((Helm, 0, 0, 0), ("hull.from_the_future", 3, 0, 0)));

            Assert.That(result.Has(ShipDesignProblemKind.UnknownPart), Is.True);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void Ids_AreUniquePositiveAndBelowTheNextId()
        {
            var floor = ShipFrameTestDesigns.Floor();
            var on = ShipPartAnchor.OnFace(ShipFrameTestDesigns.FloorFace, 0, 0);
            var duplicate = new ShipDesign("D", "", 12, new[] { new ShipPartPlacement(10, Helm, on, 0), new ShipPartPlacement(9, Block, on, 0) }, frame: floor);
            var stale = new ShipDesign("S", "", 11, new[] { new ShipPartPlacement(15, Helm, on, 0) }, frame: floor);
            var zero = new ShipDesign("Z", "", 11, new[] { new ShipPartPlacement(0, Helm, on, 0) }, frame: floor);

            Assert.That(Validate(duplicate).Has(ShipDesignProblemKind.DuplicateInstanceId), Is.True, "a part with the face's id");
            Assert.That(Validate(stale).Has(ShipDesignProblemKind.InvalidInstanceId), Is.True, "next id would reuse 15");
            Assert.That(Validate(zero).Has(ShipDesignProblemKind.InvalidInstanceId), Is.True);
        }

        [Test]
        public void Yaws_StopAt3()
        {
            Assert.That(Validate(Design((Helm, 0, 0, 4))).Has(ShipDesignProblemKind.InvalidOrientation), Is.True);
            Assert.That(Validate(Design((Helm, 0, 0, 3))).Has(ShipDesignProblemKind.InvalidOrientation), Is.False);
        }

        [Test]
        public void Flight_NeedsLiftForTheMass_AndThrust()
        {
            var heavy = Design((Helm, 0, 0, 0), (Beam, 3, 0, 0));
            var noThrust = Design((Block, 0, 0, 0), (Cairn, 3, 0, 0));

            Assert.That(Validate(heavy).Has(ShipDesignProblemKind.TooHeavy), Is.False, "the helm lifts 5000; the floor and parts weigh less");
            Assert.That(Validate(noThrust).Has(ShipDesignProblemKind.NoThrust), Is.True);
            Assert.That(Validate(noThrust).Has(ShipDesignProblemKind.TooHeavy), Is.False, "the cairn's spirit lifts it");
        }

        [Test]
        public void Limits_BoundTheExtent_ThePartCount_AndTheName()
        {
            var limits = ShipDesignLimits.Default;
            limits.MaxExtent = 4;
            limits.MaxParts = 2;
            limits.MaxNameLength = 3;

            Assert.That(Validate(Design((Helm, 6, 0, 0)), limits).Has(ShipDesignProblemKind.OutOfBounds), Is.True, "6 m out");
            Assert.That(Validate(Design((Helm, 0, 0, 0), (Block, 2, 0, 0), (Block, 3, 0, 0)), limits).Has(ShipDesignProblemKind.TooManyParts), Is.True);
            Assert.That(Validate(Design((Helm, 0, 0, 0)), limits).Has(ShipDesignProblemKind.NameTooLong), Is.True, "\"Test\"");
        }
    }
}
