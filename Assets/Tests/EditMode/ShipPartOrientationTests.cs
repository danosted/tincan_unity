#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipPartOrientation"/>: 24 distinct exact rotations, the first four being yaw turns.</summary>
    public class ShipPartOrientationTests
    {
        private static readonly Vector3Int Probe = new(1, 2, 3);

        [Test]
        public void TheTwentyFourOrientations_AreAllDifferent()
        {
            var rotated = Enumerable.Range(0, ShipPartOrientation.Count)
                .Select(o => ShipPartOrientation.Rotate(Probe, (byte)o))
                .ToList();

            Assert.That(rotated.Distinct().Count(), Is.EqualTo(ShipPartOrientation.Count));
        }

        [Test]
        public void TheFirstFour_AreQuarterTurnsAboutUp()
        {
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 0), Is.EqualTo(new ShipGridCell(1, 0, 0)));
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 1), Is.EqualTo(new ShipGridCell(0, 0, -1)));
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 2), Is.EqualTo(new ShipGridCell(-1, 0, 0)));
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 3), Is.EqualTo(new ShipGridCell(0, 0, 1)));
            for (byte o = 0; o < 4; o++)
            {
                Assert.That(ShipPartOrientation.Rotate(Vector3Int.up, o), Is.EqualTo(new ShipGridCell(0, 1, 0)), $"orientation {o} stays upright");
            }
        }

        [Test]
        public void CellRotation_MatchesTheRotationUsedToPlaceThePrefab()
        {
            for (byte o = 0; o < ShipPartOrientation.Count; o++)
            {
                var expected = Vector3Int.RoundToInt(ShipPartOrientation.ToRotation(o) * (Vector3)Probe);
                Assert.That(ShipPartOrientation.Rotate(Probe, o).ToVector3Int(), Is.EqualTo(expected), $"orientation {o}");
            }
        }

        [Test]
        public void NextYaw_TurnsWithinTheSameUpFace()
        {
            Assert.That(ShipPartOrientation.NextYaw(0), Is.EqualTo(1));
            Assert.That(ShipPartOrientation.NextYaw(3), Is.EqualTo(0));
            Assert.That(ShipPartOrientation.NextYaw(7), Is.EqualTo(4));
        }
    }
}
