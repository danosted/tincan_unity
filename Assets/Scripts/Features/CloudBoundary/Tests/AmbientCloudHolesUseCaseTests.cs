#nullable enable
using NUnit.Framework;
using TinCan.Features.CloudBoundary.Tests.Fakes;
using TinCan.Tests.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Features.CloudBoundary.Tests
{
    public class AmbientCloudHolesUseCaseTests
    {
        private CloudBoundaryConfig _boundary = null!;
        private CloudSeaConfig _config = null!;
        private FakeActorRegistry _actors = null!;
        private FakeTimeService _time = null!;
        private FakeCloudHoleState _state = null!;
        private FakeAirshipView _ship = null!;
        private AmbientCloudHolesUseCase _ambient = null!;

        [SetUp]
        public void SetUp()
        {
            _boundary = ScriptableObject.CreateInstance<CloudBoundaryConfig>();
            _config = ScriptableObject.CreateInstance<CloudSeaConfig>();
            _config.AmbientHoles = 2;
            _config.AmbientInterval = 5f;
            _actors = new FakeActorRegistry();
            _time = new FakeTimeService { Time = 10f };
            _state = new FakeCloudHoleState();
            _ship = new FakeAirshipView();
            _ship.Transform.position = new Vector3(1000f, 30f, -500f);
            _actors.Register(_state);
            _actors.Register(_ship);
            var network = new FakeNetworkService();
            var processor = new CloudHoleProcessor();
            var holes = new CloudHolesUseCase(network, _actors, _time, processor, _boundary);
            _ambient = new AmbientCloudHolesUseCase(network, _actors, _time, holes, processor, _config, new System.Random(3));
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            Object.DestroyImmediate(_boundary);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Tick_OpensOneHoleNearTheShip_ButNotUnderIt()
        {
            _ambient.Tick();

            Assert.That(_state.Holes.Count, Is.EqualTo(1));
            CloudHole hole = _state.Holes[0];
            float distance = Vector2.Distance(hole.Centre, new Vector2(1000f, -500f));
            float edge = distance - hole.Radius * (1f + _boundary.HoleRim);
            Assert.That(edge, Is.GreaterThanOrEqualTo(_config.Distance.x - 1f), "the rim stays clear of the ship");
            Assert.That(distance, Is.LessThanOrEqualTo(_config.Distance.y + _config.Radius.y * 1.5f));
            Assert.That(hole.Radius, Is.InRange(_config.Radius.x, _config.Radius.y));
            Assert.That(hole.OpenedAt, Is.EqualTo(10f));
            Assert.That(hole.ClosesAt, Is.GreaterThan(hole.OpenedAt + hole.OpenSeconds));
        }

        [Test]
        public void Tick_WaitsTheInterval_AndStopsAtTheCount()
        {
            _ambient.Tick();
            _ambient.Tick();
            Assert.That(_state.Holes.Count, Is.EqualTo(1), "one per interval");

            _time.Time = 15f;
            _ambient.Tick();
            Assert.That(_state.Holes.Count, Is.EqualTo(2));

            _time.Time = 20f;
            _ambient.Tick();
            Assert.That(_state.Holes.Count, Is.EqualTo(2), "never more than the count");
        }

        [Test]
        public void Tick_OpensNone_WhenTurnedOff()
        {
            _config.AmbientHoles = 0;

            _ambient.Tick();

            Assert.That(_state.Holes, Is.Empty);
        }
    }
}
