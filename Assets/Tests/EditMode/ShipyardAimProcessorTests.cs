#nullable enable
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipyardAimProcessor"/>: from the cursor's ray to the cell to build in and the part to remove.</summary>
    public class ShipyardAimProcessorTests
    {
        private readonly ShipyardAimProcessor _aim = new();

        [Test]
        public void OnTheTopOfADeckTile_TheNewPartGoesAbove_AndRemoveTakesTheTile()
        {
            // A deck tile in cell (2,-1,3): its top face is y -0.5.
            var ray = new ShipyardRay(new Vector3(2f, 5f, 3f), Vector3.down, true, new Vector3(2.2f, -0.5f, 2.9f), Vector3.up);

            var aim = _aim.Aim(ray);

            Assert.That(aim.PlaceCell, Is.EqualTo(new ShipGridCell(2, 0, 3)));
            Assert.That(aim.PartCell, Is.EqualTo(new ShipGridCell(2, -1, 3)));
        }

        [Test]
        public void OnTheSideOfABlock_TheNewPartGoesBesideIt()
        {
            var ray = new ShipyardRay(new Vector3(9f, 0f, 0f), Vector3.left, true, new Vector3(0.5f, 0.2f, -0.3f), Vector3.right);

            var aim = _aim.Aim(ray);

            Assert.That(aim.PlaceCell, Is.EqualTo(new ShipGridCell(1, 0, 0)));
            Assert.That(aim.PartCell, Is.EqualTo(new ShipGridCell(0, 0, 0)));
        }

        [Test]
        public void OverNothing_TheRayMeetsTheBuildFloor()
        {
            var ray = new ShipyardRay(new Vector3(3.2f, 9.5f, -1f), new Vector3(0f, -1f, 0f));

            var aim = _aim.Aim(ray);

            Assert.That(aim.IsValid, Is.True);
            Assert.That(aim.PlaceCell, Is.EqualTo(new ShipGridCell(3, 0, -1)));
            Assert.That(aim.PartCell, Is.Null);
        }

        [Test]
        public void ARayThatNeverMeetsTheFloor_AimsNowhere()
        {
            Assert.That(_aim.Aim(new ShipyardRay(new Vector3(0f, 5f, 0f), Vector3.up)).IsValid, Is.False);
            Assert.That(_aim.Aim(new ShipyardRay(new Vector3(0f, 5f, 0f), Vector3.forward)).IsValid, Is.False);
            Assert.That(_aim.Aim(new ShipyardRay(new Vector3(0f, -5f, 0f), Vector3.down)).IsValid, Is.False, "below the floor, looking down");
        }
    }
}
