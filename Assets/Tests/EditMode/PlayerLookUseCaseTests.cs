#nullable enable
using System;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Look;
using TinCan.Core.Humanoid;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="PlayerLookUseCase"/>: the mouse turns the camera of whatever the local player controls, on whichever
    /// peer. Regression: a client piloting the ship could not look around, because the ship only simulates on the server.
    /// </summary>
    public class PlayerLookUseCaseTests
    {
        private sealed class FakeLook : ILookView
        {
            public float Pitch { get; set; }
            public float Yaw { get; set; }
            public float Sensitivity => 1f;
            public float MaxPitch => 80f;
            public float AimHeight => 1.5f;
            public Camera Camera => null!;

            public void ApplyLook(float pitch, float yaw)
            {
                Pitch = pitch;
                Yaw = yaw;
            }
        }

        private sealed class FakePossessedActor : IHasLook, IPossessable
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating { get; set; }
            public ILookView Look { get; } = new FakeLook();
            public ulong? PossessorId { get; set; }
            public void AuthoritativeSetPossessor(ulong? playerId) => PossessorId = playerId;
            public bool CanPossess(ulong playerId) => true;
        }

        private sealed class FakeUnpossessableActor : IHasLook
        {
            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating { get; set; }
            public ILookView Look { get; } = new FakeLook();
        }

        private FakeInputReader _input = null!;
        private FakeNetworkService _network = null!;
        private FakeActorRegistry _actors = null!;
        private PlayerLookUseCase _look = null!;

        [SetUp]
        public void SetUp()
        {
            _input = new FakeInputReader();
            var camera = FakeInputContexts.Camera();
            _input.Vectors[camera.Look!] = new Vector2(10f, 0f);
            _network = new FakeNetworkService { LocalClientId = 1 };
            _actors = new FakeActorRegistry();
            _look = new PlayerLookUseCase(_input, camera, _network, _actors);
        }

        [Test]
        public void ClientPilot_TurnsTheShipCamera_ThoughTheServerSimulatesTheShip()
        {
            var ship = new FakePossessedActor { IsSimulating = false, PossessorId = 1 };
            _actors.Register(ship);

            _look.Tick();

            Assert.That(ship.Look.Yaw, Is.EqualTo(10f));
        }

        [Test]
        public void SomeoneElsesActor_IsNotTurned_EvenWhereItSimulates()
        {
            var otherPilot = new FakePossessedActor { IsSimulating = true, PossessorId = 2 };
            _actors.Register(otherPilot);

            _look.Tick();

            Assert.That(otherPilot.Look.Yaw, Is.Zero);
        }

        [Test]
        public void UnpossessableCamera_TurnsOnlyWhereItSimulates()
        {
            var simulating = new FakeUnpossessableActor { IsSimulating = true };
            var idle = new FakeUnpossessableActor { IsSimulating = false };
            _actors.Register(simulating);
            _actors.Register(idle);

            _look.Tick();

            Assert.That(simulating.Look.Yaw, Is.EqualTo(10f));
            Assert.That(idle.Look.Yaw, Is.Zero);
        }
    }
}
