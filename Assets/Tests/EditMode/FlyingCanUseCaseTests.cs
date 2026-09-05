#nullable enable
using System.Linq;
using NUnit.Framework;
using TinCan.Features.Airship;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers the server-side can loop: spawn cadence and cap, motion, expiry, and the driven-only gate.
    /// </summary>
    public class FlyingCanUseCaseTests
    {
        private FakeNetworkService _network = null!;
        private FakeActorRegistry _registry = null!;
        private FakeTimeService _time = null!;
        private FakeFlyingCanSpawner _spawner = null!;
        private FlyingCanConfig _config = null!;
        private FlyingCanUseCase _useCase = null!;
        private FakeAirshipView _airship = null!;

        [SetUp]
        public void SetUp()
        {
            _network = new FakeNetworkService();
            _registry = new FakeActorRegistry();
            _time = new FakeTimeService { DeltaTime = 1f, Time = 0f };
            _spawner = new FakeFlyingCanSpawner(_registry);
            _config = ScriptableObject.CreateInstance<FlyingCanConfig>();
            _config.SpawnInterval = 2f;
            _config.MaxAlive = 2;
            _config.Lifetime = 5f;
            _config.CanSpeed = 8f;
            _config.SpawnOnlyWhileDriven = false;
            _useCase = new FlyingCanUseCase(_network, _registry, _time, _spawner, new FlyingCanWaveProcessor(), new FlyingCanMotionProcessor(), _config, new System.Random(1));

            _airship = new FakeAirshipView("Airship");
            _airship.PossessorId = 1;
            _airship.InputState = new AirshipInputState { Throttle = 1f };
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
        public void Tick_SpawnsWhenIntervalElapsed_AndRespectsMaxAlive()
        {
            _config.Lifetime = 100f; // keep both cans alive so the cap is what limits spawning
            TickTimes(1);
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(0));

            TickTimes(1);
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(1));

            TickTimes(2);
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(2));

            TickTimes(4);
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(2), "cap of 2 alive");
        }

        [Test]
        public void Tick_SpawnedCan_IsAheadOfShipAndMovesBackwardEachTick()
        {
            TickTimes(2);
            var can = _spawner.Spawned.Single();
            float zAfterSpawn = can.Transform.position.z;
            Assert.That(zAfterSpawn, Is.GreaterThan(50f));

            TickTimes(1);

            Assert.That(can.Transform.position.z, Is.EqualTo(zAfterSpawn - 8f).Within(0.001f));
        }

        [Test]
        public void Tick_ExpiredCans_AreDespawned_AndSlotsReopen()
        {
            TickTimes(2);
            var first = _spawner.Spawned.Single();

            TickTimes(6); // lifetime 5

            Assert.That(_spawner.Despawned, Does.Contain(first));
            Assert.That(_registry.GetActors<IFlyingCanView>().Count(), Is.LessThanOrEqualTo(_config.MaxAlive));
        }

        [Test]
        public void Tick_SpawnOnlyWhileDriven_BlocksSpawnsWhenIdle()
        {
            _config.SpawnOnlyWhileDriven = true;
            _airship.InputState = new AirshipInputState { Throttle = 0f };

            TickTimes(5);
            Assert.That(_spawner.Spawned, Is.Empty);

            _airship.InputState = new AirshipInputState { Throttle = 1f };
            TickTimes(2);
            Assert.That(_spawner.Spawned.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_Disabled_NeverSpawns()
        {
            _config.Enabled = false;

            TickTimes(10);

            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void Tick_NoAirship_NeverSpawns()
        {
            _registry.Unregister(_airship);

            TickTimes(10);

            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void Tick_SpawnerRefuses_DoesNotResetCadence()
        {
            _spawner.RefuseSpawns = true;
            TickTimes(3);
            _spawner.RefuseSpawns = false;

            TickTimes(1);

            Assert.That(_spawner.Spawned.Count, Is.EqualTo(1));
        }

        private void TickTimes(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _time.Time += _time.DeltaTime;
                _useCase.Tick();
            }
        }
    }
}
