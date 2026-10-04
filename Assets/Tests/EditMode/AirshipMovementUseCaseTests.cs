#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Ship;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="AirshipMovementUseCase"/>: the ship takes its input from its pilot (the first source with an answer)
    /// and keeps it as its current input; with no pilot it gets none and coasts.
    /// </summary>
    public class AirshipMovementUseCaseTests
    {
        private sealed class FixedPilot : IAirshipPilotInput
        {
            public AirshipInputState? Answer;

            public bool TryGetInput(IAirshipView ship, out AirshipInputState input)
            {
                input = Answer ?? default;
                return Answer != null;
            }
        }

        private FakeActorRegistry _actors = null!;
        private FakeAirshipView _ship = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _ship = new FakeAirshipView();
            _actors.Register(_ship);
        }

        [TearDown]
        public void TearDown() => _ship.Destroy();

        private AirshipMovementUseCase Create(params IAirshipPilotInput[] pilots) => new(
            new FakeInputReader(), new FakeNetworkService(), _actors, new FakeTimeService(), new AirshipMovementProcessor(), pilots);

        [Test]
        public void NoPilot_TheShipGetsNoInput()
        {
            _ship.InputState = new AirshipInputState { Throttle = 1f };

            Create().Tick();

            Assert.That(_ship.InputState.Throttle, Is.Zero);
        }

        [Test]
        public void TheFirstPilotWithAnAnswer_Steers()
        {
            var silent = new FixedPilot();
            var helm = new FixedPilot { Answer = new AirshipInputState { Throttle = 1f, Yaw = -1f } };
            var other = new FixedPilot { Answer = new AirshipInputState { Throttle = -1f } };

            Create(silent, helm, other).Tick();

            Assert.That(_ship.InputState.Throttle, Is.EqualTo(1f));
            Assert.That(_ship.InputState.Yaw, Is.EqualTo(-1f));
        }

        [Test]
        public void APushHeadOn_MovesTheShipOut_AndStopsItDrivingIn()
        {
            var movement = FlyAhead(out var helm);
            Vector3 before = _ship.Transform.position;

            movement.Push(_ship, new Vector3(0f, 0f, -0.5f));

            Assert.That(_ship.Transform.position, Is.EqualTo(before + new Vector3(0f, 0f, -0.5f)));
            helm.Answer = default(AirshipInputState);
            movement.Tick();
            Assert.That(_ship.AppliedVelocity.z, Is.LessThanOrEqualTo(0f), "no velocity left into the rock (a little bounce back)");
        }

        [Test]
        public void APushFromTheSide_TakesOnlyTheVelocityIntoTheSurface()
        {
            var movement = FlyAhead(out var helm);
            float ahead = _ship.AppliedVelocity.z;

            movement.Push(_ship, new Vector3(0.2f, 0f, 0f));
            helm.Answer = default(AirshipInputState);
            movement.Tick();

            Assert.That(_ship.AppliedVelocity.z, Is.GreaterThan(ahead * 0.5f), "still flying along the rock");
        }

        [Test]
        public void ANoPush_ChangesNothing()
        {
            var movement = FlyAhead(out _);
            Vector3 before = _ship.Transform.position;

            movement.Push(_ship, Vector3.zero);

            Assert.That(_ship.Transform.position, Is.EqualTo(before));
        }

        /// <summary>Full throttle for a while: the ship flies along +z.</summary>
        private AirshipMovementUseCase FlyAhead(out FixedPilot helm)
        {
            helm = new FixedPilot { Answer = new AirshipInputState { Throttle = 1f } };
            var movement = Create(helm);
            for (int i = 0; i < 120; i++) movement.Tick();
            Assume.That(_ship.AppliedVelocity.z, Is.GreaterThan(1f));
            return movement;
        }
    }
}
