#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Core.Humanoid;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Features.Anchor.Tests
{
    /// <summary>The anchor's rope ladder: climbing it (<see cref="LadderProcessor"/>, <see cref="LadderClimbUseCase"/>) and landing on deck at the top (<see cref="LadderLandingUseCase"/>).</summary>
    public class LadderTests
    {
        // A ladder from the deck's height (10 m) down to 2 m, at x = 0; the deck spot is 2 m inboard of it.
        private static readonly AnchorLadder Ladder = new(new Vector3(0f, 10f, 0f), new Vector3(0f, 2f, 0f), new Vector3(2f, 10f, 0f), Quaternion.identity);

        private sealed class FakeAnchor : IAnchor
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating { get; set; } = true;
            public Transform? Ship => null;
            public bool IsDown { get; set; } = true;
            public AnchorLadder? Ladder { get; set; } = LadderTests.Ladder;
            public void ServerSetDown(bool down) => IsDown = down;
        }

        private sealed class RecordingRespawn : IHumanoidRespawnService
        {
            public int Calls { get; private set; }

            public void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation)
            {
                Calls++;
                character.Movement.SetPose(position, rotation);
            }
        }

        private sealed class SilentPublisher : IEventPublisher
        {
            public void Publish<TEvent>(TEvent evt) { }
        }

        private FakeActorRegistry _actors = null!;
        private FakeAnchor _anchor = null!;
        private FakeHumanoidMovementView _body = null!;
        private FakeHumanoidCharacterView _player = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _anchor = new FakeAnchor();
            _actors.Register(_anchor);
            _body = new FakeHumanoidMovementView("Climber");
            _player = new FakeHumanoidCharacterView(_body);
            _actors.Register(_player);
        }

        [TearDown]
        public void TearDown() => _body.Destroy();

        [Test]
        public void Forward_OnTheLadder_ClimbsUp()
        {
            Assert.That(LadderProcessor.TryClimb(new Vector3(0.3f, 5f, 0f), Ladder, 1f, false, false, out var velocity), Is.True);
            Assert.That(velocity.y, Is.EqualTo(LadderProcessor.ClimbSpeed).Within(1e-4f));
            Assert.That(velocity.x, Is.LessThan(0f), "drawn onto the ladder's line");
        }

        [Test]
        public void Back_GoesDown_AndNoInput_HangsStill()
        {
            LadderProcessor.TryClimb(new Vector3(0f, 5f, 0f), Ladder, -1f, false, false, out var down);
            LadderProcessor.TryClimb(new Vector3(0f, 5f, 0f), Ladder, 0f, false, false, out var still);

            Assert.That(down.y, Is.EqualTo(-LadderProcessor.ClimbSpeed).Within(1e-4f));
            Assert.That(still, Is.EqualTo(Vector3.zero));
        }

        [Test]
        public void Jumping_LetsGo()
        {
            Assert.That(LadderProcessor.TryClimb(new Vector3(0f, 5f, 0f), Ladder, 1f, true, false, out _), Is.False);
        }

        [Test]
        public void OnTheGround_ABodyWalksPast_UnlessItPushesForward()
        {
            var feet = new Vector3(0.2f, 1f, 0f); // under the foot, within reach of it
            Assert.That(LadderProcessor.TryClimb(feet, Ladder, 0f, false, true, out _), Is.False);
            Assert.That(LadderProcessor.TryClimb(feet, Ladder, 1f, false, true, out _), Is.True);
        }

        [Test]
        public void OutOfReach_TheLadderDoesNothing()
        {
            Assert.That(LadderProcessor.TryClimb(new Vector3(2f, 5f, 0f), Ladder, 1f, false, false, out _), Is.False, "too far to the side");
            Assert.That(LadderProcessor.TryClimb(new Vector3(0f, -1f, 0f), Ladder, 1f, false, true, out _), Is.False, "too far below the foot");
        }

        [Test]
        public void TheClimb_TakesTheStep_OnlyWhileTheAnchorIsDown()
        {
            var climb = new LadderClimbUseCase(_actors);
            _body.Transform.position = new Vector3(0f, 5f, 0f);
            var input = new HumanoidInputState { MovementDirection = Vector3.forward };

            var step = new HumanoidMotionStep(Vector3.down * 9f, Vector3.zero, 0.02f, false, false);
            climb.Contribute(_player, input, ref step);
            Assert.That(step.Velocity.y, Is.EqualTo(LadderProcessor.ClimbSpeed).Within(1e-4f));

            _anchor.IsDown = false;
            step = new HumanoidMotionStep(Vector3.down * 9f, Vector3.zero, 0.02f, false, false);
            climb.Contribute(_player, input, ref step);
            Assert.That(step.Velocity, Is.EqualTo(Vector3.down * 9f), "a raised anchor's ladder is gone");
        }

        [Test]
        public void ReachingTheTop_SetsTheClimberDownOnDeck()
        {
            var respawn = new RecordingRespawn();
            var landing = new LadderLandingUseCase(_actors, new FakeNetworkService(), respawn, new SilentPublisher());

            _body.Transform.position = new Vector3(0f, 6f, 0f);
            landing.Tick();
            Assert.That(respawn.Calls, Is.Zero, "still climbing");

            _body.Transform.position = new Vector3(0.2f, 9.9f, 0f);
            landing.Tick();
            Assert.That(respawn.Calls, Is.EqualTo(1));
            Assert.That(_body.Transform.position, Is.EqualTo(Ladder.DeckPosition));
        }

        [Test]
        public void StandingOnDeck_IsNotClimbing()
        {
            var respawn = new RecordingRespawn();
            var landing = new LadderLandingUseCase(_actors, new FakeNetworkService(), respawn, new SilentPublisher());

            _body.Transform.position = Ladder.DeckPosition;
            landing.Tick();

            Assert.That(respawn.Calls, Is.Zero);
            Assert.That(LadderProcessor.IsOn(Ladder.DeckPosition, Ladder), Is.False);
        }
    }
}
