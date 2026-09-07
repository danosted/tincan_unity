#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel.Minigame;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class FlyingCanProcessorTests
    {
        private static readonly FlyingCanSpawnParameters Parameters = new(4f, 6f, -2f, -1f, 0f);

        [Test]
        public void ShouldSpawn_RequiresTravelDistanceIncludingReverseAndHeight()
        {
            var waves = new FlyingCanWaveProcessor();
            Assert.That(waves.ShouldSpawn(Vector3.zero, Vector3.forward * 19.9f, 20f), Is.False);
            Assert.That(waves.ShouldSpawn(Vector3.zero, Vector3.back * 20f, 20f), Is.True);
            Assert.That(waves.ShouldSpawn(Vector3.zero, Vector3.up * 20f, 20f), Is.True);
            Assert.That(waves.ShouldSpawn(Vector3.zero, Vector3.zero, 0f), Is.False);
        }

        [Test]
        public void ComputeSpawn_RotatedShip_UsesWorldPositionAndLocalOffsets()
        {
            var waves = new FlyingCanWaveProcessor();
            var position = waves.ComputeSpawn(new Vector3(10f, 5f, 10f), Quaternion.Euler(0f, 90f, 0f), 60f, 0f, 1f, 0.5f, 1f, Parameters);
            Assert.That(position.x, Is.EqualTo(70f).Within(0.001f));
            Assert.That(position.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(position.z, Is.EqualTo(6f).Within(0.001f));
        }

        [Test]
        public void ComputeSpawn_NegativeSide_MirrorsOffsetAndClampsRandomInputs()
        {
            var waves = new FlyingCanWaveProcessor();
            var right = waves.ComputeSpawn(Vector3.zero, Quaternion.identity, 0f, 2f, -1f, 0.5f, 1f, Parameters);
            var left = waves.ComputeSpawn(Vector3.zero, Quaternion.identity, 0f, 2f, -1f, 0.5f, -1f, Parameters);
            Assert.That(right, Is.EqualTo(new Vector3(6f, -2f, 0f)));
            Assert.That(left, Is.EqualTo(new Vector3(-6f, -2f, 0f)));
        }

        [TestCase(0f, 1f, 0f)]
        [TestCase(0f, -1f, 0f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(1f, 0f, 0f)]
        public void TravelRotation_UsesTravelDirectionEvenForVerticalFlight(float x, float y, float z)
        {
            Vector3 direction = new(x, y, z);
            Quaternion rotation = new FlyingCanWaveProcessor().TravelRotation(direction, Quaternion.identity);
            Assert.That(Vector3.Distance(rotation * Vector3.forward, direction), Is.LessThan(0.001f));
        }

        [Test]
        public void ComputeSpawn_DepthSpread_StaggersForwardDistanceAndClampsInputs()
        {
            var waves = new FlyingCanWaveProcessor();
            var parameters = new FlyingCanSpawnParameters(0f, 55f, -25f, 25f, 15f);
            var near = waves.ComputeSpawn(Vector3.zero, Quaternion.identity, 60f, 0f, 0f, -1f, 1f, parameters);
            var far = waves.ComputeSpawn(Vector3.zero, Quaternion.identity, 60f, 1f, 1f, 2f, -1f, parameters);
            Assert.That(near, Is.EqualTo(new Vector3(0f, -25f, 45f)));
            Assert.That(far, Is.EqualTo(new Vector3(-55f, 25f, 75f)));
        }

        [Test]
        public void IsClearOfShip_ExclusionWorksInAllDirectionsAndAtBoundary()
        {
            var waves = new FlyingCanWaveProcessor();
            Vector3 centre = new(10f, 40f, -20f);
            Assert.That(waves.IsClearOfShip(centre + Vector3.up * 49f, centre, 50f), Is.False);
            Assert.That(waves.IsClearOfShip(centre + Vector3.left * 50f, centre, 50f), Is.True);
            Assert.That(waves.IsClearOfShip(centre + Vector3.back * 60f, centre, 50f), Is.True);
        }
    }
}
