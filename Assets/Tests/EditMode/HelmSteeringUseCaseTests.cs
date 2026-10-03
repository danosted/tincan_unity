#nullable enable
using NUnit.Framework;
using TinCan.Core.Humanoid;
using TinCan.Features.Helm;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="HelmSteeringUseCase"/>: a ship is steered by whoever occupies its own helm, from the station axes the
    /// server last simulated for them; an empty helm, or a helm on another ship, gives no input.
    /// </summary>
    public class HelmSteeringUseCaseTests
    {
        private FakeActorRegistry _actors = null!;
        private FakeStationOccupancy _occupancy = null!;
        private FakeAirshipView _ship = null!;
        private FakeHelm _helm = null!;
        private FakeHumanoidMovementView _body = null!;
        private FakeHumanoidCharacterView _helmsman = null!;
        private HelmSteeringUseCase _useCase = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _occupancy = new FakeStationOccupancy();
            _ship = new FakeAirshipView();
            _helm = new FakeHelm(_ship);
            _actors.Register(_helm);
            _body = new FakeHumanoidMovementView("Helmsman");
            _helmsman = new FakeHumanoidCharacterView(_body);
            _useCase = new HelmSteeringUseCase(_actors, _occupancy);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            _body.Destroy();
        }

        [Test]
        public void EmptyHelm_GivesNoInput()
        {
            Assert.That(_useCase.TryGetInput(_ship, out _), Is.False);
        }

        [Test]
        public void MannedHelm_SteersWithTheHelmsmansAxes()
        {
            _occupancy.TryOccupy(_helmsman, _helm);
            _helmsman.InputState = new HumanoidInputState { StationAxes = new Vector3(1f, -0.5f, 0.25f) };

            Assert.That(_useCase.TryGetInput(_ship, out var input), Is.True);
            Assert.That(input.Throttle, Is.EqualTo(1f));
            Assert.That(input.Yaw, Is.EqualTo(-0.5f));
            Assert.That(input.Pitch, Is.EqualTo(0.25f));
        }

        [Test]
        public void MannedHelm_WithNoKeysHeld_StillAnswers_SoTheShipCoasts()
        {
            _occupancy.TryOccupy(_helmsman, _helm);

            Assert.That(_useCase.TryGetInput(_ship, out var input), Is.True);
            Assert.That(input.Throttle, Is.Zero);
        }

        [Test]
        public void AHelmOnAnotherShip_DoesNotSteerThisOne()
        {
            var other = new FakeAirshipView("Other");
            try
            {
                _occupancy.TryOccupy(_helmsman, _helm);

                Assert.That(_useCase.TryGetInput(other, out _), Is.False);
            }
            finally
            {
                other.Destroy();
            }
        }

        [Test]
        public void LeavingTheHelm_StopsTheInput()
        {
            _occupancy.TryOccupy(_helmsman, _helm);
            _occupancy.Leave(_helmsman.Id);

            Assert.That(_useCase.TryGetInput(_ship, out _), Is.False);
        }

        [Test]
        public void Steer_ClampsEachAxis()
        {
            var input = HelmSteeringUseCase.Steer(new Vector3(3f, -2f, 1.5f));

            Assert.That((input.Throttle, input.Yaw, input.Pitch), Is.EqualTo((1f, -1f, 1f)));
        }
    }
}
