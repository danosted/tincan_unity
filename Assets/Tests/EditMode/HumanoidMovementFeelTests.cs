#nullable enable
using NUnit.Framework;
using TinCan.Core.Humanoid;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// How the shipped player prefab feels to move, in simulation ticks: starting, stopping and turning around must be
    /// near immediate. In first person the whole view moves with the body, so the gentle acceleration a third-person
    /// body could get away with (walking speed after 0.23 s, a 0.6 s glide out of a sprint) reads as sluggish controls.
    /// These read the real tunables (NetworkPlayer.prefab) and tick rate (the NetworkManager), and step the same
    /// processor the simulation uses. The live counterpart is the net harness's start and stop latencies
    /// (.docs/NETWORK_TEST_HARNESS.md).
    /// </summary>
    public class HumanoidMovementFeelTests
    {
        private const string PlayerPrefab = "Assets/Prefabs/NetworkPlayer.prefab";
        private const string NetworkPrefab = "Assets/Prefabs/Singletons/NetworkService.prefab";

        private readonly HumanoidMovementProcessor _processor = new();
        private IHumanoidMovementView _body = null!;
        private float _tick;

        [SetUp]
        public void SetUp()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPrefab);
            Assert.That(player, Is.Not.Null, $"Player prefab moved: {PlayerPrefab}");
            _body = player.GetComponent<HumanoidControllerView>();

            var network = AssetDatabase.LoadAssetAtPath<GameObject>(NetworkPrefab)?.GetComponentInChildren<NetworkManager>(true);
            Assert.That(network, Is.Not.Null, $"NetworkManager prefab moved: {NetworkPrefab}");
            _tick = 1f / network!.NetworkConfig.TickRate;
        }

        private float Walk => _body.WalkSpeed;
        private float Sprint => _body.WalkSpeed * _body.SprintMultiplier;

        [Test]
        public void FromRest_ReachesWalkingSpeed_WithinThreeTicks()
        {
            Assert.That(TicksUntil(Vector3.zero, Vector3.forward, Walk, v => v.magnitude >= Walk * 0.99f), Is.LessThanOrEqualTo(3));
        }

        [Test]
        public void FromRest_TheFirstTickAlreadyMovesVisibly()
        {
            var velocity = Step(Vector3.zero, Vector3.forward, Walk);

            Assert.That(velocity.magnitude * _tick, Is.GreaterThanOrEqualTo(0.02f), "the harness's start measure: 2 cm");
        }

        [Test]
        public void Released_StopsFromWalking_WithinThreeTicks()
        {
            Assert.That(TicksUntil(Vector3.forward * Walk, Vector3.zero, Walk, v => v.magnitude < 0.01f), Is.LessThanOrEqualTo(3));
        }

        [Test]
        public void Released_StopsFromSprinting_WithinSixTicks()
        {
            Assert.That(TicksUntil(Vector3.forward * Sprint, Vector3.zero, Sprint, v => v.magnitude < 0.01f), Is.LessThanOrEqualTo(6));
        }

        [Test]
        public void Reversing_IsAtFullSpeedTheOtherWay_WithinSixTicks()
        {
            Assert.That(TicksUntil(Vector3.forward * Walk, Vector3.back, Walk, v => Vector3.Dot(v, Vector3.back) >= Walk * 0.99f), Is.LessThanOrEqualTo(6));
        }

        [Test]
        public void Strafing_TurnsTheVelocityWithinFourTicks()
        {
            Assert.That(TicksUntil(Vector3.forward * Walk, Vector3.right, Walk, v => Vector3.Angle(v, Vector3.right) < 5f), Is.LessThanOrEqualTo(4));
        }

        private Vector3 Step(Vector3 velocity, Vector3 direction, float speed) =>
            _processor.CalculateHorizontalVelocity(velocity, direction, speed, _body.Acceleration, _body.Deceleration, _tick);

        private int TicksUntil(Vector3 velocity, Vector3 direction, float speed, System.Func<Vector3, bool> done)
        {
            for (int tick = 1; tick <= 60; tick++)
            {
                velocity = Step(velocity, direction, speed);
                if (done(velocity)) return tick;
            }
            return int.MaxValue;
        }
    }
}
