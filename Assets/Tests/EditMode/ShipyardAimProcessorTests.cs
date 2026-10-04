#nullable enable
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipyardAimProcessor"/>: new parts go on the working level; Remove takes the part the ray hits.</summary>
    public class ShipyardAimProcessorTests
    {
        private readonly ShipyardAimProcessor _aim = new();

        [Test]
        public void TheCell_IsWhereTheRayMeetsTheLevelsFloor()
        {
            var down = new ShipyardRay(new Vector3(3.2f, 9.5f, -1f), Vector3.down);

            Assert.That(_aim.Aim(down, 0).PlaceCell, Is.EqualTo(new ShipGridCell(3, 0, -1)));
            Assert.That(_aim.Aim(down, -1).PlaceCell, Is.EqualTo(new ShipGridCell(3, -1, -1)));
            Assert.That(_aim.Aim(down, 4).PlaceCell, Is.EqualTo(new ShipGridCell(3, 4, -1)));
        }

        [Test]
        public void ASlantedRay_MeetsEachLevelFurtherOn()
        {
            // From (0, 10.5, 0) along (1, -1, 0): level 0's floor (y -0.5) at x 11, level 5's (y 4.5) at x 6.
            var slant = new ShipyardRay(new Vector3(0f, 10.5f, 0f), new Vector3(1f, -1f, 0f).normalized);

            Assert.That(_aim.Aim(slant, 0).PlaceCell, Is.EqualTo(new ShipGridCell(11, 0, 0)));
            Assert.That(_aim.Aim(slant, 5).PlaceCell, Is.EqualTo(new ShipGridCell(6, 5, 0)));
        }

        [Test]
        public void ThePartUnderTheCursor_IsBehindTheFaceTheRayHits()
        {
            var onDeckTop = new ShipyardRay(new Vector3(2f, 5f, 3f), Vector3.down, true, new Vector3(2.2f, -0.5f, 2.9f), Vector3.up);
            var onBlockSide = new ShipyardRay(new Vector3(9f, 0f, 0f), Vector3.left, true, new Vector3(0.5f, 0.2f, -0.3f), Vector3.right);

            Assert.That(_aim.Aim(onDeckTop, 0).PartCell, Is.EqualTo(new ShipGridCell(2, -1, 3)));
            Assert.That(_aim.Aim(onBlockSide, 0).PartCell, Is.EqualTo(new ShipGridCell(0, 0, 0)));
        }

        [Test]
        public void ARayThatNeverMeetsTheLevel_PlacesNowhere_ButCanStillRemove()
        {
            Assert.That(_aim.Aim(new ShipyardRay(new Vector3(0f, 5f, 0f), Vector3.up), 0).IsValid, Is.False);
            Assert.That(_aim.Aim(new ShipyardRay(new Vector3(0f, 5f, 0f), Vector3.forward), 0).IsValid, Is.False);

            var upAtABlock = new ShipyardRay(new Vector3(0f, -5f, 0f), Vector3.up, true, new Vector3(0f, -0.5f, 0f), Vector3.down);
            var aim = _aim.Aim(upAtABlock, -8);
            Assert.That(aim.IsValid, Is.False, "level -8 is behind the camera");
            Assert.That(aim.PartCell, Is.EqualTo(new ShipGridCell(0, 0, 0)));
        }
    }
}
