#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="ShipPartOrientation"/>: four quarter yaws, exact, matching the rotation the prefab gets.</summary>
    public class ShipPartOrientationTests
    {
        private static readonly Vector3Int Probe = new(1, 2, 3);

        [Test]
        public void TheFourYaws_AreQuarterTurnsAboutUp_AndAllDifferent()
        {
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 0), Is.EqualTo(new Vector3Int(1, 0, 0)));
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 1), Is.EqualTo(new Vector3Int(0, 0, -1)));
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 2), Is.EqualTo(new Vector3Int(-1, 0, 0)));
            Assert.That(ShipPartOrientation.Rotate(new Vector3Int(1, 0, 0), 3), Is.EqualTo(new Vector3Int(0, 0, 1)));
            Assert.That(Enumerable.Range(0, ShipPartOrientation.Count).Select(o => ShipPartOrientation.Rotate(Probe, (byte)o)).Distinct().Count(), Is.EqualTo(4));
            Assert.That(ShipPartOrientation.IsValid(4), Is.False);
        }

        [Test]
        public void BlockRotation_MatchesTheRotationUsedToPlaceThePrefab()
        {
            for (byte o = 0; o < ShipPartOrientation.Count; o++)
            {
                var expected = Vector3Int.RoundToInt(ShipPartOrientation.ToRotation(o) * (Vector3)Probe);
                Assert.That(ShipPartOrientation.Rotate(Probe, o), Is.EqualTo(expected), $"yaw {o}");
            }
        }

        [Test]
        public void NextYaw_GoesRound()
        {
            Assert.That(ShipPartOrientation.NextYaw(0), Is.EqualTo(1));
            Assert.That(ShipPartOrientation.NextYaw(3), Is.EqualTo(0));
        }
    }
}
