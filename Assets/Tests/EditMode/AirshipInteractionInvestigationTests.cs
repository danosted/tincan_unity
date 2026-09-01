using NUnit.Framework;
using UnityEngine;
using TinCan.Features.Airship;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Abilities;
using TinCan.Tests.EditMode.Fakes;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Investigates the theorized root cause of the platform-riding position jump (see repo notes):
    /// whether ResolveGrounding's two detection paths (raycast hit vs. ParentLocalSpaceVolume overlap)
    /// can disagree on where the ship's IMovingGround component lives, dropping platform tracking.
    /// </summary>
    public class AirshipInteractionInvestigationTests
    {
        private FakeTimeService _timeService;
        private FakeActorRegistry _actorRegistry;
        private HumanoidMovementProcessor _processor;
        private AbilitySystemUseCase _abilitySystem;
        private HumanoidMovementUseCase _useCase;

        private FakeHumanoidMovementView _movementView;
        private FakeHumanoidCharacterView _character;
        private GameObject _shipRoot;

        [SetUp]
        public void SetUp()
        {
            _timeService = new FakeTimeService { DeltaTime = 1f / 30f };
            _actorRegistry = new FakeActorRegistry();
            _processor = new HumanoidMovementProcessor();
            _abilitySystem = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actorRegistry, _timeService, new FakeEventPublisher());
            _useCase = new HumanoidMovementUseCase(new FakeInputService(), new FakeNetworkService(), _processor, _abilitySystem, _actorRegistry, _timeService);

            _shipRoot = new GameObject("ShipRoot");

            _movementView = new FakeHumanoidMovementView("FakeCharacter") { Gravity = 0f };
            _character = new FakeHumanoidCharacterView(_movementView);
            _actorRegistry.Register(_character);
        }

        [TearDown]
        public void TearDown()
        {
            _movementView.Destroy();
            UnityEngine.Object.DestroyImmediate(_shipRoot);
        }

        [Test]
        public void VolumeDetection_WorksWhenMovingGroundIsOnParentNotSameObjectAsVolume()
        {
            // IMovingGround lives on the ship root (typical: velocity is computed at the root),
            // while the boarding/interior trigger volume is a separate child object below it.
            _shipRoot.AddComponent<FakeMovingGround>();

            var interiorVolume = new GameObject("InteriorVolume");
            interiorVolume.transform.SetParent(_shipRoot.transform);
            var volumeCollider = interiorVolume.AddComponent<BoxCollider>();
            volumeCollider.isTrigger = true;
            volumeCollider.size = new Vector3(4f, 4f, 4f);
            interiorVolume.AddComponent<ParentLocalSpaceVolume>();

            // Character stands inside the interior volume but not above any raycast-solid collider
            // (e.g. mid-air inside the hull), so only the volume-overlap path can detect the ship.
            _movementView.Transform.position = _shipRoot.transform.position;
            Physics.SyncTransforms();

            _useCase.Tick();

            Assert.That(_movementView.CurrentGround.MovingGroundTransform, Is.EqualTo(_shipRoot.transform));
        }

        [Test]
        public void VolumeDetection_WorksWhenMovingGroundIsOnSameObjectAsVolume()
        {
            var interiorVolume = new GameObject("InteriorVolume");
            interiorVolume.transform.SetParent(_shipRoot.transform);
            var volumeCollider = interiorVolume.AddComponent<BoxCollider>();
            volumeCollider.isTrigger = true;
            volumeCollider.size = new Vector3(4f, 4f, 4f);
            interiorVolume.AddComponent<ParentLocalSpaceVolume>();
            interiorVolume.AddComponent<FakeMovingGround>(); // Same object as the volume this time.

            _movementView.Transform.position = _shipRoot.transform.position;
            Physics.SyncTransforms();

            _useCase.Tick();

            Assert.That(_movementView.CurrentGround.MovingGroundTransform, Is.EqualTo(interiorVolume.transform));
        }

        [Test]
        public void RotatingPlatform_YawIsIsolatedFromPitchAndRoll()
        {
            var platformCollider = _shipRoot.AddComponent<BoxCollider>();
            platformCollider.size = new Vector3(20f, 1f, 20f);
            _shipRoot.transform.position = new Vector3(0f, -0.5f, 0f);
            _shipRoot.AddComponent<FakeMovingGround>();

            _movementView.Transform.position = new Vector3(0f, 0.01f, 0f);
            Physics.SyncTransforms();

            _useCase.Tick(); // Caches initial pose.

            // Bank the ship: yaw (turn) + pitch/roll (banking tilt), mimicking AirshipControllerView.
            _shipRoot.transform.rotation = Quaternion.Euler(15f, 20f, 10f);
            Physics.SyncTransforms();

            _useCase.Tick();

            // Only the yaw component should be applied to the character's own rotation.
            float characterYaw = _movementView.Transform.rotation.eulerAngles.y;
            Assert.That(characterYaw, Is.EqualTo(20f).Within(0.01f));
            Assert.That(_movementView.Transform.rotation.eulerAngles.x, Is.EqualTo(0f).Within(0.01f));
            Assert.That(_movementView.Transform.rotation.eulerAngles.z, Is.EqualTo(0f).Within(0.01f));
        }
    }
}
