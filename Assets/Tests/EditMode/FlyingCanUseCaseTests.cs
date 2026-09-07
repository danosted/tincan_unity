#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class FlyingCanUseCaseTests
    {
        private FakeActorRegistry _registry = null!;
        private FakeFlyingCanSpawner _spawner = null!;
        private FlyingCanConfig _config = null!;
        private FlyingCanUseCase _useCase = null!;
        private FakeAirshipView _airship = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new FakeActorRegistry();
            _spawner = new FakeFlyingCanSpawner(_registry);
            _config = ScriptableObject.CreateInstance<FlyingCanConfig>();
            // Narrow deterministic volume for cadence/retention tests; default scatter is checked separately.
            _config.InitialAheadDistance = 0f;
            _config.MinimumShipDistance = 0f;
            _config.DepthSpread = 0f;
            _config.LateralMin = 4.5f;
            _config.LateralMax = 6.5f;
            _config.HeightMin = -2.5f;
            _config.HeightMax = -1f;
            _config.MinimumSeparation = 6f;
            _useCase = new FlyingCanUseCase(new FakeNetworkService(), _registry, _spawner,
                new FlyingCanWaveProcessor(), _config, new System.Random(1));
            _airship = new FakeAirshipView("Airship");
            _registry.Register(_airship);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner.DestroyAll();
            _airship.Destroy();
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void Tick_ConfiguredNarrowVolume_SeedsBothSidesWithoutDriver()
        {
            _airship.Transform.position = new Vector3(10f, 20f, 30f);
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(14));
            var local = _spawner.Spawned.Select(can => _airship.Transform.InverseTransformPoint(can.Transform.position)).ToArray();
            Assert.That(local.Count(p => Mathf.Abs(p.z) < 0.01f), Is.EqualTo(2));
            Assert.That(local.Count(p => Mathf.Abs(p.z - 120f) < 0.01f), Is.EqualTo(2));
            Assert.That(local.Count(p => p.x < 0f), Is.EqualTo(7));
            Assert.That(local.All(p => p.y >= -2.5f && p.y <= -1f), Is.True);
        }

        [Test]
        public void Tick_ParkedOrRotating_CansStayFixedAndNeverExpireOrAccumulate()
        {
            _useCase.Tick();
            var positions = _spawner.Spawned.Select(c => c.Transform.position).ToArray();
            for (int i = 0; i < 10000; i++)
            {
                _airship.Transform.rotation = Quaternion.Euler(0f, i, 0f);
                _useCase.Tick();
            }
            Assert.That(_spawner.Spawned.Select(c => c.Transform.position), Is.EqualTo(positions));
            Assert.That(_spawner.Despawned, Is.Empty);
        }

        [Test]
        public void Tick_TravelReachesSpacing_AddsPairAtHorizonWithoutMovingExistingCans()
        {
            _useCase.Tick();
            var positions = _spawner.Spawned.Select(c => c.Transform.position).ToArray();
            _airship.Transform.position = Vector3.forward * 19.9f;
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(14));
            _airship.Transform.position = Vector3.forward * 20f;
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(16));
            Assert.That(_spawner.Spawned.Take(14).Select(c => c.Transform.position), Is.EqualTo(positions));
            Assert.That(_spawner.Spawned.Skip(14).All(c => Mathf.Abs(c.Transform.position.z - 140f) < 0.01f), Is.True);
        }

        [TestCase(1f, 0f, 0f)]
        [TestCase(0f, 0f, -1f)]
        [TestCase(0f, 1f, 0f)]
        public void Tick_TravelChangesDirection_SpawnsInActualTravelDirection(float x, float y, float z)
        {
            _useCase.Tick();
            Vector3 direction = new(x, y, z);
            _airship.Transform.position = direction * 20f;
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(16));
            foreach (var can in _spawner.Spawned.Skip(14))
                Assert.That(Vector3.Dot(can.Transform.position - _airship.Transform.position, direction), Is.EqualTo(120f).Within(0.01f));
        }

        [Test]
        public void Tick_LongJourney_ContinuesSpawningWithBoundedPopulation()
        {
            _useCase.Tick();
            for (int i = 1; i <= 100; i++)
            {
                _airship.Transform.position = Vector3.forward * i * 20f;
                _useCase.Tick();
                Assert.That(_registry.GetActors<IFlyingCanView>().Count(), Is.LessThanOrEqualTo(_config.MaxAlive));
            }
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(214));
            Assert.That(_spawner.Despawned.Count, Is.GreaterThan(150));
            Assert.That(_registry.GetActors<IFlyingCanView>().All(c => Vector3.Distance(c.Transform.position, _airship.Transform.position) <= 200f), Is.True);
        }

        [Test]
        public void Tick_CapReached_KeepsNearbyPickupsAndResumesAfterLeavingArea()
        {
            _config.MaxAlive = 2;
            _useCase.Tick();
            _airship.Transform.position = Vector3.forward * 20f;
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(2));
            Assert.That(_spawner.Despawned, Is.Empty);
            _airship.Transform.position = Vector3.forward * 240f;
            _useCase.Tick();
            Assert.That(_spawner.Despawned.Count, Is.EqualTo(2));
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(4));
            Assert.That(_registry.GetActors<IFlyingCanView>().All(c => c.Transform.position.z > 240f), Is.True);
        }

        [Test]
        public void Tick_BacktrackingIntoExistingRows_DoesNotDuplicateThem()
        {
            _useCase.Tick();
            for (int i = 1; i <= 7; i++)
            {
                _airship.Transform.position = Vector3.forward * i * 20f;
                _useCase.Tick();
            }
            int count = _spawner.Spawned.Count;
            _airship.Transform.position = Vector3.forward * 120f; // reverse horizon reaches initial pair at z=0
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(count));
        }

        [Test]
        public void Tick_PickupNearAnotherShip_IsRetained()
        {
            var other = new FakeAirshipView("Other");
            try
            {
                _registry.Register(other);
                _useCase.Tick();
                var first = _spawner.Spawned.First();
                _airship.Transform.position = Vector3.forward * 240f;
                _useCase.Tick();
                Assert.That(_spawner.Despawned, Has.No.Member(first));
            }
            finally { _registry.Unregister(other); other.Destroy(); }
        }

        [Test]
        public void Tick_NoShipThenNewShip_SeedsAtNewLocation()
        {
            _registry.Unregister(_airship);
            _useCase.Tick();
            Assert.That(_spawner.Spawned, Is.Empty);
            _airship.Transform.position = Vector3.right * 1000f;
            _registry.Register(_airship);
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(14));
            Assert.That(_spawner.Spawned.All(c => c.Transform.position.x > 990f), Is.True);
        }

        [Test]
        public void Tick_Disabled_DoesNotSpawn()
        {
            _config.Enabled = false;
            _useCase.Tick();
            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void Tick_SpawnerRefuses_RetriesInitialAndTravelRows()
        {
            _spawner.RefuseSpawns = true;
            _useCase.Tick();
            Assert.That(_spawner.Spawned, Is.Empty);
            _spawner.RefuseSpawns = false;
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(14));
            _airship.Transform.position = Vector3.forward * 20f;
            _spawner.RefuseSpawns = true;
            _useCase.Tick();
            _spawner.RefuseSpawns = false;
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(16));
        }

        [Test]
        public void Tick_DefaultScatter_LeavesShipClearAndVariesAllThreeAxes()
        {
            var defaults = ScriptableObject.CreateInstance<FlyingCanConfig>();
            try
            {
                var useCase = new FlyingCanUseCase(new FakeNetworkService(), _registry, _spawner,
                    new FlyingCanWaveProcessor(), defaults, new System.Random(1));
                _airship.Transform.SetPositionAndRotation(new Vector3(10f, 40f, 30f), Quaternion.Euler(10f, 70f, 15f));
                useCase.Tick();
                var local = _spawner.Spawned.Select(c => _airship.Transform.InverseTransformPoint(c.Transform.position)).ToArray();
                Assert.That(local.Length, Is.EqualTo(8));
                Assert.That(local.All(p => p.magnitude >= defaults.MinimumShipDistance), Is.True);
                Assert.That(local.Max(p => p.x) - local.Min(p => p.x), Is.GreaterThan(40f));
                Assert.That(local.Min(p => p.y), Is.LessThan(-5f));
                Assert.That(local.Max(p => p.y), Is.GreaterThan(5f));
                Assert.That(local.Select(p => Mathf.Round(p.z)).Distinct().Count(), Is.GreaterThan(4));
                Assert.That(local.All(p => p.z >= 45f && p.z <= 135f), Is.True);

                var before = _spawner.Spawned.Select(c => c.Transform.position).ToArray();
                _airship.Transform.position += _airship.Transform.forward * 21f;
                useCase.Tick();
                Assert.That(_spawner.Spawned.Take(before.Length).Select(c => c.Transform.position), Is.EqualTo(before));
                Assert.That(_spawner.Spawned.Count, Is.EqualTo(10));
                foreach (var can in _spawner.Spawned.Skip(before.Length))
                {
                    var point = _airship.Transform.InverseTransformPoint(can.Transform.position);
                    Assert.That(point.z, Is.InRange(105f, 135f));
                    Assert.That(point.magnitude, Is.GreaterThanOrEqualTo(50f));
                }
            }
            finally { Object.DestroyImmediate(defaults); }
        }

        [Test]
        public void Tick_SpawnVolumeInsideShipExclusion_SkipsCansWithoutMovingExistingOnes()
        {
            _useCase.Tick();
            var before = _spawner.Spawned.Select(c => c.Transform.position).ToArray();
            _config.MinimumShipDistance = 1000f;
            _airship.Transform.position = Vector3.forward * 20f;
            _useCase.Tick();
            Assert.That(_spawner.Spawned.Select(c => c.Transform.position), Is.EqualTo(before));
        }

        [Test]
        public void Tick_AnotherShipOccupiesInitialVolume_SpawnsOutsideBothShips()
        {
            _config.MinimumShipDistance = 50f;
            var other = new FakeAirshipView("Other");
            try
            {
                other.Transform.position = Vector3.forward * 60f;
                _registry.Register(other);
                _useCase.Tick();
                Assert.That(_spawner.Spawned, Is.Not.Empty);
                Assert.That(_spawner.Spawned.All(c => Vector3.Distance(c.Transform.position, _airship.Transform.position) >= 50f
                    && Vector3.Distance(c.Transform.position, other.Transform.position) >= 50f), Is.True);
            }
            finally { _registry.Unregister(other); other.Destroy(); }
        }
    }
}
