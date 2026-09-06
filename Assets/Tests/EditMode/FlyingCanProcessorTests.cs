#nullable enable
using NUnit.Framework;
using TinCan.Features.Airship.Fuel.Minigame;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class FlyingCanProcessorTests
    {
        private static readonly FlyingCanSpawnParameters Parameters = new(
            aheadDistance: 60f, lateralMin: 4f, lateralMax: 6f, heightMin: -2f, heightMax: -1f, canSpeed: 8f);

        [Test]
        public void ShouldSpawn_RequiresIntervalElapsedAndRoomForAnotherCan()
        {
            var waves = new FlyingCanWaveProcessor();

            Assert.That(waves.ShouldSpawn(4f, 0, 4f, 4), Is.True);
            Assert.That(waves.ShouldSpawn(3.9f, 0, 4f, 4), Is.False);
            Assert.That(waves.ShouldSpawn(10f, 4, 4f, 4), Is.False);
        }

        [Test]
        public void ComputeSpawn_PlacesCanAheadOfShipInShipLocalSpace()
        {
            var waves = new FlyingCanWaveProcessor();
            var shipRotation = Quaternion.Euler(0f, 90f, 0f); // ship forward = world +X
            var shipPosition = new Vector3(10f, 5f, 10f);

            var (position, velocity) = waves.ComputeSpawn(shipPosition, shipRotation, lateral01: 0f, height01: 1f, side: 1f, Parameters);

            // local (4, -1, 60) rotated 90deg about Y -> world (60, -1, -4)
            Assert.That(position.x, Is.EqualTo(70f).Within(0.001f));
            Assert.That(position.y, Is.EqualTo(4f).Within(0.001f));
            Assert.That(position.z, Is.EqualTo(6f).Within(0.001f));

            // velocity opposes ship forward at CanSpeed
            Assert.That(velocity.x, Is.EqualTo(-8f).Within(0.001f));
            Assert.That(velocity.magnitude, Is.EqualTo(8f).Within(0.001f));
        }

        [Test]
        public void ComputeSpawn_NegativeSide_MirrorsLateralOffset()
        {
            var waves = new FlyingCanWaveProcessor();

            var (right, _) = waves.ComputeSpawn(Vector3.zero, Quaternion.identity, 1f, 0f, 1f, Parameters);
            var (left, _) = waves.ComputeSpawn(Vector3.zero, Quaternion.identity, 1f, 0f, -1f, Parameters);

            Assert.That(right.x, Is.EqualTo(6f).Within(0.001f));
            Assert.That(left.x, Is.EqualTo(-6f).Within(0.001f));
            Assert.That(right.z, Is.EqualTo(60f).Within(0.001f));
        }

        [Test]
        public void Motion_StepMovesByVelocityAndExpiresAfterLifetime()
        {
            var motion = new FlyingCanMotionProcessor();

            Vector3 next = motion.Step(new Vector3(0f, 0f, 10f), new Vector3(0f, 0f, -8f), 0.5f);
            Assert.That(next.z, Is.EqualTo(6f).Within(0.001f));

            Assert.That(motion.IsExpired(spawnTime: 10f, now: 24f, lifetime: 15f), Is.False);
            Assert.That(motion.IsExpired(spawnTime: 10f, now: 25f, lifetime: 15f), Is.True);
            Assert.That(motion.IsExpired(spawnTime: 10f, now: 1000f, lifetime: 0f), Is.False, "lifetime 0 means never expire");
        }
    }
}
