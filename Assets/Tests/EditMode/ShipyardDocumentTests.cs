#nullable enable
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using TinCan.Tests.EditMode.Fakes;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipyardDocument"/>: edits with undo and redo, and the saved flag.</summary>
    public class ShipyardDocumentTests
    {
        private ShipDesignTestParts _parts = null!;

        [SetUp]
        public void SetUp() => _parts = new ShipDesignTestParts();

        [TearDown]
        public void TearDown() => _parts.Dispose();

        private ShipyardDocument Document() =>
            new(Design((Helm, 0, 0, 0, 0)), new ShipDesignEditProcessor(), _parts.Catalog, ShipDesignLimits.Default);

        [Test]
        public void UndoAndRedo_WalkTheHistory()
        {
            var document = Document();
            var start = Parts(document.Design);
            document.Apply(ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 0));
            document.Apply(ShipDesignEdit.Place(Block, new ShipGridCell(2, 0, 0), 0));
            var end = Parts(document.Design);

            Assert.That(document.Undo() && document.Undo(), Is.True);
            Assert.That(Parts(document.Design), Is.EqualTo(start), "the parts; the next id stays: ids are never reused");
            Assert.That(document.Undo(), Is.False, "nothing left to undo");

            Assert.That(document.Redo() && document.Redo(), Is.True);
            Assert.That(Parts(document.Design), Is.EqualTo(end));
            Assert.That(document.Redo(), Is.False);
        }

        private static string[] Parts(ShipDesign design) => System.Linq.Enumerable.ToArray(System.Linq.Enumerable.Select(design.Parts, p => p.ToString()));

        [Test]
        public void ANewEdit_ClearsRedo()
        {
            var document = Document();
            document.Apply(ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 0));
            document.Undo();

            document.Apply(ShipDesignEdit.Place(Block, new ShipGridCell(-1, 0, 0), 0));

            Assert.That(document.RedoCount, Is.Zero);
        }

        [Test]
        public void ARefusedEdit_ChangesNothing_AndTryNeverChangesAnything()
        {
            var document = Document();
            var before = document.Design;

            Assert.That(document.Try(ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 0)).Applied, Is.True);
            Assert.That(document.Apply(ShipDesignEdit.Place(Block, new ShipGridCell(0, 1, 0), 0)).Applied, Is.False, "the helm's top cell");

            Assert.That(document.Design, Is.SameAs(before));
            Assert.That((document.UndoCount, document.IsDirty), Is.EqualTo((0, false)));
        }

        [Test]
        public void EditsAndRenames_MakeItDirty_SavingAndOpeningClearIt()
        {
            var document = Document();
            document.Apply(ShipDesignEdit.Place(Block, new ShipGridCell(1, 0, 0), 0));
            Assert.That(document.IsDirty, Is.True);

            document.MarkSaved();
            Assert.That(document.IsDirty, Is.False);

            document.Rename("Other");
            Assert.That((document.Design.Name, document.IsDirty), Is.EqualTo(("Other", true)));

            document.Open(Design((Helm, 0, 0, 0, 0)));
            Assert.That((document.IsDirty, document.UndoCount), Is.EqualTo((false, 0)));
        }
    }
}
