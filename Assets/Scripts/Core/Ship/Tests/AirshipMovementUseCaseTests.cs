#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Ship;
using UnityEngine;
using TinCan.Tests.Fakes;

namespace TinCan.Core.Ship.Tests
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

        private sealed class FixedForce : IAirshipFlightForces
        {
            public AirshipFlightForce Force;

            public AirshipFlightForce Evaluate(IAirshipView ship, float engineSpeed, float momentum, float deltaTime) => Force;
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

        private AirshipMovementUseCase Create(params IAirshipPilotInput[] pilots) => Create(pilots, System.Array.Empty<IAirshipFlightForces>());

        private AirshipMovementUseCase Create(IAirshipPilotInput[] pilots, IAirshipFlightForces[] forces) =>
            Create(pilots, forces, System.Array.Empty<IAirshipHold>());

        private AirshipMovementUseCase Create(IAirshipPilotInput[] pilots, IAirshipFlightForces[] forces, IAirshipHold[] holds) => new(
            new FakeInputReader(), new FakeNetworkService(), _actors, new FakeTimeService(), new AirshipMovementProcessor(), pilots, forces, holds);

        private sealed class SwitchHold : IAirshipHold
        {
            public bool Holding;
            public bool Holds(IAirshipView ship) => Holding;
        }

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

        [Test]
        public void AFlightForce_AddsMomentumOnTheNose_AndAVerticalPush()
        {
            var helm = new FixedPilot { Answer = new AirshipInputState { Throttle = 1f } };
            var force = new FixedForce { Force = new AirshipFlightForce(momentumChange: 0.05f, vertical: -2f, noseDrop: 0f) };
            var withForce = Create(new IAirshipPilotInput[] { helm }, new IAirshipFlightForces[] { force });
            for (int i = 0; i < 120; i++) withForce.Tick();

            Assert.That(_ship.AppliedVelocity.z, Is.GreaterThan(_ship.MaxForwardSpeed + 2f), "faster than the engine alone");
            Assert.That(_ship.AppliedVelocity.y, Is.LessThan(-1f), "sinking");
        }

        [Test]
        public void ANoseDrop_PitchesTheNoseDown_WithoutBuildingUp()
        {
            var force = new FixedForce { Force = new AirshipFlightForce(0f, 0f, noseDrop: 5f) };
            var movement = Create(System.Array.Empty<IAirshipPilotInput>(), new IAirshipFlightForces[] { force });

            movement.Tick();
            movement.Tick();
            movement.Tick();

            Assert.That(_ship.AppliedAngularVelocity.x, Is.EqualTo(5f).Within(1e-4f));
        }

        [Test]
        public void AFlightForcesLevel_IsWhereTheNoseEasesWithNoPitchInput()
        {
            // The sea slopes away ahead: level is 6 degrees nose down, and the level ship's nose turns down towards it.
            var force = new FixedForce { Force = new AirshipFlightForce(0f, 0f, noseDrop: 0f, level: 6f) };
            var movement = Create(System.Array.Empty<IAirshipPilotInput>(), new IAirshipFlightForces[] { force });

            movement.Tick();

            Assert.That(_ship.AppliedAngularVelocity.x, Is.GreaterThan(0f));
        }

        [Test]
        public void AHeldShip_StandsStill_DespiteThrottleAndFlightForces()
        {
            var helm = new FixedPilot { Answer = new AirshipInputState { Throttle = 1f, Yaw = 1f } };
            var force = new FixedForce { Force = new AirshipFlightForce(momentumChange: 0.5f, vertical: -3f, noseDrop: 5f) };
            var hold = new SwitchHold { Holding = true };
            var movement = Create(new IAirshipPilotInput[] { helm }, new IAirshipFlightForces[] { force }, new IAirshipHold[] { hold });

            for (int i = 0; i < 60; i++) movement.Tick();

            Assert.That(_ship.AppliedVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_ship.AppliedAngularVelocity, Is.EqualTo(Vector3.zero));
            Assert.That(_ship.InputState.Throttle, Is.EqualTo(1f), "the pilot's input is still kept on the ship");
        }

        [Test]
        public void LettingGo_StartsFromRest_WithNoStoredMomentum()
        {
            var helm = new FixedPilot { Answer = new AirshipInputState { Throttle = 1f } };
            var force = new FixedForce { Force = new AirshipFlightForce(momentumChange: 0.5f, vertical: 0f, noseDrop: 0f) };
            var hold = new SwitchHold();
            var movement = Create(new IAirshipPilotInput[] { helm }, new IAirshipFlightForces[] { force }, new IAirshipHold[] { hold });
            for (int i = 0; i < 60; i++) movement.Tick();
            float flying = _ship.AppliedVelocity.z;

            hold.Holding = true;
            movement.Tick();
            hold.Holding = false;
            helm.Answer = default(AirshipInputState);
            force.Force = default;
            movement.Tick();

            Assert.That(_ship.AppliedVelocity.magnitude, Is.LessThan(flying * 0.25f), "no leap away with the old speed");
        }

        [Test]
        public void Unpiloted_TheNoseLevelsFasterThanWhenAPilotLetsGo()
        {
            // Nose 20 degrees down (the fake never turns, so the tilt stays): a pilot letting go eases it at the
            // ship's pitch level speed, an empty helm much faster.
            _ship.Transform.rotation = Quaternion.Euler(20f, 0f, 0f);
            var helm = new FixedPilot { Answer = default(AirshipInputState) };
            var manned = Create(helm);
            for (int i = 0; i < 90; i++) manned.Tick();
            float mannedPitch = _ship.AppliedAngularVelocity.x;

            helm.Answer = null;
            for (int i = 0; i < 90; i++) manned.Tick();

            Assert.That(mannedPitch, Is.LessThan(0f), "the nose comes up either way");
            Assert.That(_ship.AppliedAngularVelocity.x, Is.LessThan(mannedPitch - 2f));
        }

        [Test]
        public void Unpiloted_TheForcesMomentumFades_SoADiveDoesNotCarryOn()
        {
            var helm = new FixedPilot { Answer = new AirshipInputState { Throttle = 1f } };
            var dive = new FixedForce { Force = new AirshipFlightForce(momentumChange: 0.05f, vertical: 0f, noseDrop: 0f) };
            var movement = Create(new IAirshipPilotInput[] { helm }, new IAirshipFlightForces[] { dive });
            for (int i = 0; i < 120; i++) movement.Tick();
            Assume.That(_ship.AppliedVelocity.z, Is.GreaterThan(_ship.MaxForwardSpeed + 2f));

            helm.Answer = null;
            dive.Force = default;
            for (int i = 0; i < 150; i++) movement.Tick();

            Assert.That(_ship.AppliedVelocity.z, Is.LessThan(1f), "engine and momentum both wound down");
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
