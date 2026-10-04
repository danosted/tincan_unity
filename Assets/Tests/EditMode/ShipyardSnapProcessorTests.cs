#nullable enable
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using TinCan.Tests.EditMode.Fakes;
using static TinCan.Tests.EditMode.Fakes.ShipDesignTestParts;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipyardSnapProcessor"/>: parts go onto the ship, never floating; near misses snap to its edge.</summary>
    public class ShipyardSnapProcessorTests
    {
        private ShipDesignTestParts _parts = null!;
        private readonly ShipyardSnapProcessor _snap = new();

        // A helm at the origin (cells (0,0,0) and (0,1,0)) on a beam along x at level -1 (cells -1..1).
        private static readonly ShipDesign Ship = Design((Helm, 0, 0, 0, 0), (Beam, -1, -1, 0, 0));

        [SetUp]
        public void SetUp() => _parts = new ShipDesignTestParts();

        [TearDown]
        public void TearDown() => _parts.Dispose();

        private bool Snap(ShipGridCell aimed, out ShipGridCell cell, string part = Block, ShipDesign? design = null) =>
            _snap.TrySnap(design ?? Ship, _parts.Catalog, _parts[part], 0, aimed, out cell);

        [Test]
        public void ACellTouchingTheShip_IsUsedAsAimed()
        {
            Assert.That(Snap(new ShipGridCell(1, 0, 0), out var beside), Is.True);
            Assert.That(beside, Is.EqualTo(new ShipGridCell(1, 0, 0)), "on the beam, beside the helm");

            Assert.That(Snap(new ShipGridCell(2, -1, 0), out var end), Is.True);
            Assert.That(end, Is.EqualTo(new ShipGridCell(2, -1, 0)), "the beam's end");
        }

        [Test]
        public void ANearMiss_SnapsToTheNearestTouchingCell_OnTheSameLevel()
        {
            Assert.That(Snap(new ShipGridCell(2, 0, 1), out var cell), Is.True);

            Assert.That(cell, Is.EqualTo(new ShipGridCell(1, 0, 0)), "one step diagonally, above the beam");
        }

        [Test]
        public void FarFromTheShip_NothingFits_AndTheAimedCellComesBack()
        {
            Assert.That(Snap(new ShipGridCell(6, 0, 6), out var cell), Is.False);
            Assert.That(cell, Is.EqualTo(new ShipGridCell(6, 0, 6)));

            Assert.That(Snap(new ShipGridCell(0, 2, 0), out _), Is.True, "on top of the helm's upper cell (0,1,0)");
            Assert.That(Snap(new ShipGridCell(0, 3, 0), out _), Is.False, "a level higher: nothing there to touch");
        }

        [Test]
        public void AnEmptyDesign_TakesTheFirstPartAnywhere()
        {
            Assert.That(Snap(new ShipGridCell(9, 9, 9), out var cell, design: ShipDesign.Empty("E")), Is.True);
            Assert.That(cell, Is.EqualTo(new ShipGridCell(9, 9, 9)));
        }

        [Test]
        public void ACellAlreadyTaken_IsNeverUsed()
        {
            Assert.That(Snap(new ShipGridCell(0, 0, 0), out var cell), Is.True);
            Assert.That(cell, Is.Not.EqualTo(new ShipGridCell(0, 0, 0)), "the helm is there");
        }
    }
}
