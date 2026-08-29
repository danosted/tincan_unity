#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Events;
using TinCan.Features.Airship;
using TinCan.Features.CloudBoundary;
using TinCan.Features.HumanoidMovement;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class CloudBoundaryUseCaseTests
    {
        private sealed class RecordingEventPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();

            public void Publish<TEvent>(TEvent evt)
            {
                Events.Add(evt!);
            }
        }

        private sealed class FakeRespawnService : IHumanoidRespawnService
        {
            public int CallCount { get; private set; }

            public void ResetCharacter(IHumanoidCharacterView character, Vector3 position, Quaternion rotation)
            {
                CallCount++;
                character.Movement.SetPose(position, rotation);
                character.InputState = default;
            }
        }

        private sealed class FakeAirshipView : IAirshipView
        {
            private readonly GameObject _gameObject;

            public FakeAirshipView(string name, Vector3 position)
            {
                _gameObject = new GameObject(name);
                Transform.position = position;
            }

            public Guid Id { get; } = Guid.NewGuid();
            public bool IsSimulating => true;
            public AirshipInputState InputState { get; set; }
            public ulong? PossessorId { get; private set; }
            public Transform Transform => _gameObject.transform;
            public float MaxForwardSpeed => 10f;
            public float MaxBackwardSpeed => 5f;
            public float AccelerationRate => 5f;
            public float DecelerationRate => 5f;
            public float AngularAcceleration => 5f;
            public float AngularDeceleration => 5f;
            public float VelocityBlendRate => 1f;
            public float TurnSpeed => 30f;
            public float PitchSpeed => 20f;
            public float MaxBankAngle => 20f;
            public float BankSpeed => 1f;
            public Vector3 Velocity => Vector3.zero;
            public Vector3 PositionDelta => Vector3.zero;
            public Quaternion RotationDelta => Quaternion.identity;
            public bool IsControlsEnabled => true;

            public void Destroy() => UnityEngine.Object.DestroyImmediate(_gameObject);
            public void AuthoritativeSetPossessor(ulong? playerId) => PossessorId = playerId;
            public bool CanPossess(ulong playerId) => true;
            public void ApplyMovement(Vector3 velocity, Vector3 angularVelocity) { }
            public void Simulate(float deltaTime) { }
            public Vector3 GetPointVelocity(Vector3 worldPoint) => Vector3.zero;
            public void EnableControls() { }
            public void DisableControls() { }
        }

        private CloudBoundaryConfig _config = null!;
        private FakeActorRegistry _registry = null!;
        private FakeTimeService _timeService = null!;
        private RecordingEventPublisher _events = null!;
        private FakeRespawnService _respawnService = null!;
        private CloudBoundaryUseCase _useCase = null!;
        private readonly List<FakeAirshipView> _airships = new();
        private readonly List<FakeHumanoidMovementView> _movementViews = new();

        [SetUp]
        public void SetUp()
        {
            _config = ScriptableObject.CreateInstance<CloudBoundaryConfig>();
            _registry = new FakeActorRegistry();
            _timeService = new FakeTimeService { DeltaTime = 1f };
            _events = new RecordingEventPublisher();
            _respawnService = new FakeRespawnService();
            _useCase = new CloudBoundaryUseCase(
                new FakeNetworkService(),
                _registry,
                _timeService,
                _events,
                new CloudSurfaceQuery(_config),
                new NoOpCloudBoundaryExpiryHandler(),
                _respawnService,
                new CloudBoundaryProcessor(),
                _config);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (FakeAirshipView airship in _airships) airship.Destroy();
            foreach (FakeHumanoidMovementView movement in _movementViews) movement.Destroy();
            UnityEngine.Object.DestroyImmediate(_config);
        }

        [Test]
        public void AirshipBelowBoundary_FiresStartedThenSingleExpiryEvent()
        {
            AddAirship(new Vector3(0f, -6f, 0f));

            for (int tick = 0; tick < 17; tick++)
            {
                _useCase.Tick();
            }

            Assert.That(_events.Events.FindAll(evt => evt is CloudEmergencyStartedEvent), Has.Count.EqualTo(1));
            Assert.That(_events.Events.FindAll(evt => evt is CloudEmergencyExpiredEvent), Has.Count.EqualTo(1));
        }

        [Test]
        public void AirshipRecoversAboveMargin_FiresClearedAndRestartsFullCountdown()
        {
            FakeAirshipView airship = AddAirship(new Vector3(0f, -6f, 0f));
            _useCase.Tick();
            airship.Transform.position = new Vector3(0f, 3f, 0f);

            _useCase.Tick();

            Assert.That(_events.Events.Exists(evt => evt is CloudEmergencyClearedEvent), Is.True);
        }

        [Test]
        public void FallenCharacter_ResetsAboveNearestAirshipUsingFallbackOffset()
        {
            AddAirship(new Vector3(100f, 20f, 0f));
            FakeAirshipView nearest = AddAirship(new Vector3(10f, 20f, 0f));
            var movement = new FakeHumanoidMovementView("FallenCharacter");
            movement.Transform.position = new Vector3(0f, -11f, 0f);
            _movementViews.Add(movement);
            var character = new FakeHumanoidCharacterView(movement);
            _registry.Register(character);

            _useCase.Tick();

            Assert.That(_respawnService.CallCount, Is.EqualTo(1));
            Assert.That(movement.Transform.position, Is.EqualTo(nearest.Transform.TransformPoint(new Vector3(0f, 3f, 0f))));
            Assert.That(_events.Events.Exists(evt => evt is CloudCharacterResetEvent), Is.True);
        }

        private FakeAirshipView AddAirship(Vector3 position)
        {
            var airship = new FakeAirshipView("Airship", position);
            _airships.Add(airship);
            _registry.Register(airship);
            return airship;
        }
    }
}
