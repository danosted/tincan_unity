#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Ship;
using TinCan.Tests.EditMode.Fakes;

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
    }
}
