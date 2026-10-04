#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Tests.EditMode.Fakes;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipDesignEditProcessor"/>: edits, their refusals, and undo through the returned inverse.</summary>
    public class ShipDesignEditProcessorTests
    {
        private ShipDesignTestParts _parts = null!;
        private readonly ShipDesignEditProcessor _edits = new();

        [SetUp]
        public void SetUp() => _parts = new ShipDesignTestParts();

        [TearDown]
        public void TearDown() => _parts.Dispose();

        private ShipDesignEditResult Apply(ShipDesign design, ShipDesignEdit edit, ShipDesignLimits? limits = null) =>
            _edits.Apply(design, edit, _parts.Catalog, limits ?? ShipDesignLimits.Default);

        [Test]
        public void Place_AddsThePart_WithTheNextId()
        {
            var result = Apply(ShipDesign.Empty("New"), ShipDesignEdit.Place(Helm, new ShipGridCell(0, 0, 0), 1));

            Assert.That(result.Applied, Is.True, result.ToString());
            Assert.That(result.AddedInstanceId, Is.EqualTo(1));
            Assert.That(result.Design.NextPartInstanceId, Is.EqualTo(2));
            var placed = result.Design.Parts.Single();
            Assert.That((placed.PartId, placed.Cell, placed.Orientation), Is.EqualTo((Helm, new ShipGridCell(0, 0, 0), (byte)1)));
        }

        [Test]
        public void Place_IsRefused_ForAnUnknownPart_ATakenCell_OrABadOrientation()
        {
            var design = Design((Helm, 0, 0, 0, 0));

            Assert.That(Apply(design, ShipDesignEdit.Place("hull.nope", new ShipGridCell(1, 0, 0), 0)).Applied, Is.False);
            Assert.That(Apply(design, ShipDesignEdit.Place(Block, new ShipGridCell(0, 1, 0), 0)).Applied, Is.False, "the helm's top cell");
            Assert.That(Apply(design, ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 24)).Applied, Is.False);
            var refused = Apply(design, ShipDesignEdit.Place(Beam, new ShipGridCell(-2, 0, 0), 0));
            Assert.That(refused.Applied, Is.False, "the beam's far end reaches the helm");
            Assert.That(refused.Design, Is.SameAs(design));
            Assert.That(refused.Refusal, Does.Contain("taken"));
        }

        [Test]
        public void Place_IsRefused_BeyondTheLimits()
        {
            var limits = ShipDesignLimits.Default;
            limits.MaxExtent = 2;
            limits.MaxParts = 1;

            Assert.That(Apply(ShipDesign.Empty("E"), ShipDesignEdit.Place(Beam, new ShipGridCell(1, 0, 0), 0), limits).Applied, Is.False);
            Assert.That(Apply(Design((Helm, 0, 0, 0, 0)), ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 0), limits).Applied, Is.False);
        }

        [Test]
        public void Remove_ThenItsUndo_RestoresThePart_WithItsOwnId()
        {
            var design = Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 2));

            var removed = Apply(design, ShipDesignEdit.Remove(2));
            Assert.That(removed.Applied, Is.True);
            Assert.That(removed.Design.Parts.Select(p => p.InstanceId), Is.EqualTo(new[] { 1 }));

            var undone = Apply(removed.Design, removed.Undo!);
            Assert.That(undone.Applied, Is.True, undone.ToString());
            Assert.That(ShipDesignHash.Compute(undone.Design), Is.EqualTo(ShipDesignHash.Compute(design)));
        }

        [Test]
        public void UndoOfAPlace_RemovesIt_AndRedoBringsBackTheSameId()
        {
            var design = Design((Helm, 0, 0, 0, 0));
            var placed = Apply(design, ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 0));

            var undone = Apply(placed.Design, placed.Undo!);
            var redone = Apply(undone.Design, undone.Undo!);

            Assert.That(undone.Design.Parts.Count, Is.EqualTo(1));
            Assert.That(redone.Design.Parts.Last().InstanceId, Is.EqualTo(placed.AddedInstanceId));
        }

        [Test]
        public void Ids_AreNeverReused_AfterARemove()
        {
            var design = Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0));
            var removed = Apply(design, ShipDesignEdit.Remove(2)).Design;

            var placed = Apply(removed, ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 0));

            Assert.That(placed.AddedInstanceId, Is.EqualTo(3));
        }

        [Test]
        public void RemovingAMissingPart_OrRestoringAPresentOne_IsRefused()
        {
            var design = Design((Helm, 0, 0, 0, 0));

            Assert.That(Apply(design, ShipDesignEdit.Remove(9)).Applied, Is.False);
            Assert.That(Apply(design, ShipDesignEdit.Restore(design.Parts[0])).Applied, Is.False);
        }

        [Test]
        public void Disconnecting_IsAllowed_TheValidatorReportsIt()
        {
            var design = Design((Helm, 0, 0, 0, 0), (Block, 1, 0, 0, 0), (Block, 2, 0, 0, 0));

            var result = Apply(design, ShipDesignEdit.Remove(2));

            Assert.That(result.Applied, Is.True);
            Assert.That(new ShipDesignValidator().Validate(result.Design, _parts.Catalog, ShipDesignLimits.Default)
                .Has(ShipDesignProblemKind.Disconnected), Is.True);
        }
    }
}
