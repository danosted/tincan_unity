#nullable enable
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipDesignValidator"/>: every rule a flyable design keeps.</summary>
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

        [Test]
        public void AHelmOnABeam_IsValid()
        {
            var result = Validate(Design((Beam, 0, 0, 0, 0), (Helm, 1, 1, 0, 0)));

            Assert.That(result.IsValid, Is.True, result.ToString());
            Assert.That(result.Problems, Is.Empty);
        }

        [Test]
        public void ADesignWithoutAHelm_HasNoCore()
        {
            Assert.That(Validate(Design((Block, 0, 0, 0, 0))).Has(ShipDesignProblemKind.MissingCore), Is.True);
            Assert.That(Validate(Design()).Has(ShipDesignProblemKind.MissingCore), Is.True, "an empty design cannot fly");
        }

        [Test]
        public void TwoHelms_AreOneTooMany()
        {
            var result = Validate(Design((Helm, 0, 0, 0, 0), (Helm, 1, 0, 0, 0)));

            Assert.That(result.Has(ShipDesignProblemKind.ExtraCore), Is.True);
            Assert.That(result.IsValid, Is.False);
        }

        [Test]
        public void PartsInOneCell_Overlap_RotatedFootprintsIncluded()
        {
            Assert.That(Validate(Design((Helm, 0, 0, 0, 0), (Block, 0, 1, 0, 0))).Has(ShipDesignProblemKind.Overlap), Is.True,
                "the helm's second cell");
            // A beam turned once runs along -Z: (5,0,0), (5,0,-1), (5,0,-2).
            var rotated = Design((Helm, 5, 1, 0, 0), (Beam, 5, 0, 0, 1), (Block, 5, 0, -2, 0));
            Assert.That(Validate(rotated).Has(ShipDesignProblemKind.Overlap), Is.True);
        }

        [Test]
        public void APartNotJoinedToTheHelm_IsDisconnected()
        {
            var result = Validate(Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0), (Block, 3, 0, 0, 0)));

            Assert.That(result.Has(ShipDesignProblemKind.Disconnected), Is.True);
            Assert.That(result.Problems[0].InstanceId, Is.EqualTo(3), "the far block");
        }

        [Test]
        public void TouchingOnlyAtAnEdge_IsNotJoined()
        {
            var result = Validate(Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 1, 0)));

            Assert.That(result.Has(ShipDesignProblemKind.Disconnected), Is.True);
        }

        [Test]
        public void AnUnknownPart_IsReported_ButDoesNotBlock()
        {
            var result = Validate(Design((Helm, 0, 0, 0, 0), ("hull.from_the_future", 1, 0, 0, 0)));

            Assert.That(result.Has(ShipDesignProblemKind.UnknownPart), Is.True);
            Assert.That(result.IsValid, Is.True);
        }

        [Test]
        public void InstanceIds_AreUniquePositiveAndBelowTheNextId()
        {
            var duplicate = new ShipDesign("D", "", 3, new[]
            {
                new ShipPartPlacement(1, Helm, new ShipGridCell(0, 0, 0), 0),
                new ShipPartPlacement(1, Block, new ShipGridCell(1, 0, 0), 0),
            });
            var stale = new ShipDesign("S", "", 2, new[] { new ShipPartPlacement(5, Helm, new ShipGridCell(0, 0, 0), 0) });
            var zero = new ShipDesign("Z", "", 2, new[] { new ShipPartPlacement(0, Helm, new ShipGridCell(0, 0, 0), 0) });

            Assert.That(Validate(duplicate).Has(ShipDesignProblemKind.DuplicateInstanceId), Is.True);
            Assert.That(Validate(stale).Has(ShipDesignProblemKind.InvalidInstanceId), Is.True, "next id would reuse 5");
            Assert.That(Validate(zero).Has(ShipDesignProblemKind.InvalidInstanceId), Is.True);
        }

        [Test]
        public void Orientations_StopAt23()
        {
            Assert.That(Validate(Design((Helm, 0, 0, 0, 24))).Has(ShipDesignProblemKind.InvalidOrientation), Is.True);
            Assert.That(Validate(Design((Helm, 0, 0, 0, 23))).Has(ShipDesignProblemKind.InvalidOrientation), Is.False);
        }

        [Test]
        public void Limits_BoundTheGrid_ThePartCount_AndTheName()
        {
            var limits = ShipDesignLimits.Default;
            limits.MaxExtent = 4;
            limits.MaxParts = 2;
            limits.MaxNameLength = 3;

            Assert.That(Validate(Design((Helm, 0, 4, 0, 0)), limits).Has(ShipDesignProblemKind.OutOfBounds), Is.True, "the helm's top cell is at y 5");
            Assert.That(Validate(Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0), (Block, 2, 0, 0, 0)), limits)
                .Has(ShipDesignProblemKind.TooManyParts), Is.True);
            Assert.That(Validate(Design((Helm, 0, 0, 0, 0)), limits).Has(ShipDesignProblemKind.NameTooLong), Is.True, "\"Test\"");
        }
    }
}
