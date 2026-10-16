#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Humanoid;
using TinCan.Features.Respawn;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class LeashRopeUseCaseTests
    {
        private RespawnRules _rules = null!;
        private GameplayTag _leashedTag = null!;
        private LeashStore _leashes = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeHumanoidCharacterView _player = null!;
        private Point _point = null!;

        [SetUp]
        public void SetUp()
        {
            _leashedTag = ScriptableObject.CreateInstance<GameplayTag>();
            _rules = ScriptableObject.CreateInstance<RespawnRules>();
            _rules.LeashedTag = _leashedTag;
            _rules.CanClimb = true;
            _rules.ClimbSpeed = 3f;
            _rules.WinchSpeed = 6f;
            _rules.ArriveDistance = 1.5f;
            _leashes = new LeashStore();
            _movement = new FakeHumanoidMovementView("Leashed");
            _movement.Transform.position = new Vector3(0f, -12f, 0f);
            _player = new FakeHumanoidCharacterView(_movement);
            _point = new Point();
            _leashes.Attach(_player.Id, _point, 12f);
        }

        [TearDown]
        public void TearDown()
        {
            _movement.Destroy();
            _point.Destroy();
            Object.DestroyImmediate(_rules);
            Object.DestroyImmediate(_leashedTag);
        }

        [Test]
        public void ALeashedBody_IsServerDriven()
        {
            var rope = Rope();
            Assert.That(rope.IsServerDriven(_player), Is.False);

            _player.HeldTags.Add(_leashedTag);
            Assert.That(rope.IsServerDriven(_player), Is.True, "Its owner would mispredict the rope.");
        }

        [Test]
        public void TheRope_StopsTheFall()
        {
            var step = Step(new Vector3(0f, -10f, 0f));

            Rope().Contribute(_player, default, ref step);

            Vector3 next = _movement.Transform.position + step.Velocity * step.DeltaTime;
            Assert.That(Vector3.Distance(next, _point.Anchor.position), Is.LessThanOrEqualTo(12f + 1e-3f));
        }

        [Test]
        public void HoldingForward_Climbs()
        {
            var step = Step(Vector3.zero);

            Rope().Contribute(_player, new HumanoidInputState { MovementDirection = Vector3.forward }, ref step);

            _leashes.TryGet(_player.Id, out var leash);
            Assert.That(leash.Length, Is.EqualTo(12f - 3f * step.DeltaTime).Within(1e-4f));
            Assert.That(step.Velocity.y, Is.GreaterThan(0f), "The shorter rope pulls the body up.");
        }

        [Test]
        public void AReelingWinch_ShortensTheRope_WithoutInput()
        {
            _leashes.ToggleWinch(_point);
            var step = Step(Vector3.zero);

            Rope().Contribute(_player, default, ref step);

            _leashes.TryGet(_player.Id, out var leash);
            Assert.That(leash.Length, Is.EqualTo(12f - 6f * step.DeltaTime).Within(1e-4f));
        }

        [Test]
        public void ReachingThePoint_MarksTheLeashArrived()
        {
            _movement.Transform.position = _point.Anchor.position - new Vector3(0f, 1f, 0f);
            var step = Step(new Vector3(0f, -3f, 0f));

            Rope().Contribute(_player, default, ref step);

            _leashes.TryGet(_player.Id, out var leash);
            Assert.That(leash.Arrived, Is.True);
            Assert.That(step.Velocity, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void OnAClient_OrInReplay_OrUnleashed_ItDoesNothing()
        {
            var falling = new Vector3(0f, -10f, 0f);
            var client = Step(falling);
            Rope(new FakeSessionNetworkService { IsServer = false, IsClient = true }).Contribute(_player, default, ref client);
            Assert.That(client.Velocity, Is.EqualTo(falling));

            var replay = new HumanoidMotionStep(falling, falling, 1f / 30f, isGrounded: false, isReplay: true);
            Rope().Contribute(_player, default, ref replay);
            Assert.That(replay.Velocity, Is.EqualTo(falling));

            _leashes.Detach(_player.Id);
            var free = Step(falling);
            Rope().Contribute(_player, default, ref free);
            Assert.That(free.Velocity, Is.EqualTo(falling));
        }

        private LeashRopeUseCase Rope(TinCan.Core.Domain.Networking.INetworkService? network = null) =>
            new(_rules, _leashes, network ?? new FakeNetworkService());

        private static HumanoidMotionStep Step(Vector3 velocity) => new(velocity, velocity, 1f / 30f, isGrounded: false, isReplay: false);

        private sealed class Point : ILeashPoint
        {
            private readonly GameObject _root = new("LeashPoint");

            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public Transform Anchor => _root.transform;
            public Transform Stand => _root.transform;
            public Transform? Ship => null;
            public void Destroy() => Object.DestroyImmediate(_root);
        }
    }
}
