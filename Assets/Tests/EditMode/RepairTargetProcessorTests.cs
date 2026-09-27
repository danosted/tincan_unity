#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Damage;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class RepairTargetProcessorTests
    {
        private readonly RepairTargetProcessor _processor = new();

        private int Find(params (int, Vector3, bool)[] parts) =>
            _processor.FindTarget(Vector3.zero, Vector3.forward, parts, reach: 2.5f, coneDegrees: 100f);

        [Test]
        public void PicksTheNearestBrokenPartInFront()
        {
            Assert.That(Find((0, new Vector3(0f, 1f, 2f), true), (1, new Vector3(0.3f, 0.8f, 1f), true)), Is.EqualTo(1));
        }

        [Test]
        public void IgnoresHealthyParts()
        {
            Assert.That(Find((0, new Vector3(0f, 1f, 1f), false), (1, new Vector3(0f, 1f, 2f), true)), Is.EqualTo(1));
        }

        [Test]
        public void IgnoresPartsOutOfReach()
        {
            Assert.That(Find((0, new Vector3(0f, 0.8f, 3f), true)), Is.EqualTo(-1));
        }

        [Test]
        public void IgnoresPartsBehindOrBesideThePlayer()
        {
            Assert.That(Find((0, new Vector3(0f, 1f, -1.5f), true)), Is.EqualTo(-1), "behind");
            Assert.That(Find((1, new Vector3(2f, 1f, 0.2f), true)), Is.EqualTo(-1), "outside the 100 degree cone");
        }

        [Test]
        public void HeightDoesNotAffectFacing()
        {
            Assert.That(Find((0, new Vector3(0f, 2f, 1f), true)), Is.EqualTo(0), "A part above the player still counts as in front.");
        }

        [Test]
        public void StandingOnAPart_CountsAsFacingIt()
        {
            Assert.That(Find((0, new Vector3(0f, 1f, 0f), true)), Is.EqualTo(0));
        }
    }
}
