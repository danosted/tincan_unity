using NUnit.Framework;
using UnityEngine;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Abilities;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Regression coverage for the platform-riding "position jump" bug: a humanoid standing on a
    /// moving platform should be carried by the platform's real per-tick displacement (SurfaceDelta),
    /// with no ticks silently dropped, which is what causes the corrective snap/jump on a later tick.
    /// </summary>
    public class HumanoidMovementUseCaseTests
    {
        private FakeTimeService _timeService;
        private FakeInputService _inputService;
        private FakeNetworkService _networkService;
        private FakeActorRegistry _actorRegistry;
        private FakeAbilityRegistry _abilityRegistry;
        private FakeEventPublisher _eventPublisher;
        private HumanoidMovementProcessor _processor;
        private AbilitySystemUseCase _abilitySystem;
        private HumanoidMovementUseCase _useCase;

        private FakeHumanoidMovementView _movementView;
        private FakeHumanoidCharacterView _character;
        private GameObject _platformObject;
        private FakeMovingGround _platform;

        [SetUp]
        public void SetUp()
        {
            _timeService = new FakeTimeService { DeltaTime = 1f / 30f };
            _inputService = new FakeInputService();
            _networkService = new FakeNetworkService();
            _actorRegistry = new FakeActorRegistry();
            _abilityRegistry = new FakeAbilityRegistry();
            _eventPublisher = new FakeEventPublisher();
            _processor = new HumanoidMovementProcessor();
            _abilitySystem = new AbilitySystemUseCase(_abilityRegistry, _actorRegistry, _timeService, _eventPublisher);
            _useCase = new HumanoidMovementUseCase(_inputService, _networkService, _processor, _abilitySystem, _actorRegistry, _timeService);

            // Wide, solid platform collider positioned just below the character so a downward
            // raycast hits it, and stays under the character even after a single large test-only jump.
            _platformObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _platformObject.name = "FakePlatform";
            _platformObject.transform.position = new Vector3(0f, -0.5f, 0f);
            _platformObject.transform.localScale = new Vector3(20f, 1f, 20f);
            _platform = _platformObject.AddComponent<FakeMovingGround>();

            _movementView = new FakeHumanoidMovementView("FakeCharacter") { Gravity = 0f };
            _movementView.Transform.position = new Vector3(0f, 0.01f, 0f);
            _character = new FakeHumanoidCharacterView(_movementView);
            _actorRegistry.Register(_character);

            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            _movementView.Destroy();
            UnityEngine.Object.DestroyImmediate(_platformObject);
        }

        [Test]
        public void FirstTickOnPlatform_HasNoSurfaceDelta()
        {
            _useCase.Tick();

            Assert.That(_movementView.CurrentGround.SurfaceDelta, Is.EqualTo(Vector3.zero));
            Assert.That(_movementView.CurrentGround.MovingGroundTransform, Is.EqualTo(_platformObject.transform));
        }

        [Test]
        public void SubsequentTick_CarriesCharacterByPlatformsRealDisplacement()
        {
            _useCase.Tick(); // First tick: caches the platform's pose, no delta applied yet.

            Vector3 positionBeforeMove = _movementView.Transform.position;
            var platformMotion = new Vector3(0f, 0f, 1f);
            _platformObject.transform.position += platformMotion;
            Physics.SyncTransforms();

            _useCase.Tick(); // Second tick: should carry the character by the platform's real delta.

            Assert.That(Vector3.Distance(_movementView.CurrentGround.SurfaceDelta, platformMotion), Is.LessThan(0.0001f));
            Assert.That(Vector3.Distance(_movementView.Transform.position, positionBeforeMove + platformMotion), Is.LessThan(0.0001f));
        }

        [Test]
        public void ContinuousRide_NeverDropsATicksWorthOfMotion()
        {
            _useCase.Tick(); // First tick: caches pose.

            Vector3 totalPlatformMotion = Vector3.zero;
            Vector3 positionBeforeMoving = _movementView.Transform.position;

            for (int i = 0; i < 5; i++)
            {
                var step = new Vector3(0.2f, 0f, 0.3f);
                _platformObject.transform.position += step;
                totalPlatformMotion += step;
                Physics.SyncTransforms();
                _useCase.Tick();
            }

            float distance = Vector3.Distance(_movementView.Transform.position, positionBeforeMoving + totalPlatformMotion);
            Assert.That(distance, Is.LessThan(0.0001f));
        }
    }
}
