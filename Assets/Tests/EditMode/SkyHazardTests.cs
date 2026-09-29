#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Features.SkyHazards;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary><see cref="SkyHazardUseCase"/> and <see cref="SkyHazardFieldProcessor"/> (plan <c>cannon-and-hazards.md</c>, S1 targets).</summary>
    public class SkyHazardTests
    {
        private sealed class FakeHazard : ISkyHazard
        {
            public readonly GameObject GameObject = new("Hazard");
            public Transform? Transform => GameObject != null ? GameObject.transform : null;
            public float Health01 => IsDestroyed ? 0f : 1f;
            public bool IsDestroyed { get; set; }
        }

        private sealed class FakeSpawner : ISkyHazardSpawner
        {
            public List<FakeHazard> Spawned { get; } = new();
            public List<ISkyHazard> Despawned { get; } = new();

            public ISkyHazard? Spawn(Vector3 position)
            {
                var hazard = new FakeHazard();
                hazard.GameObject.transform.position = position;
                Spawned.Add(hazard);
                return hazard;
            }

            public void Despawn(ISkyHazard hazard) => Despawned.Add(hazard);
        }

        private readonly SkyHazardFieldProcessor _field = new();
        private FakeTimeService _time = null!;
        private FakeActorRegistry _actors = null!;
        private FakeSpawner _spawner = null!;
        private SkyHazardConfig _config = null!;
        private FakeAirshipView _ship = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService { DeltaTime = 0.5f };
            _actors = new FakeActorRegistry();
            _spawner = new FakeSpawner();
            _config = ScriptableObject.CreateInstance<SkyHazardConfig>();
            _config.MaxAlive = 3;
            _config.SpawnInterval = 1f;
            _config.DespawnDelay = 1f;
            _config.RemoveDistance = 500f;
            _ship = new FakeAirshipView();
            _actors.Register(_ship);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var hazard in _spawner.Spawned) Object.DestroyImmediate(hazard.GameObject);
            Object.DestroyImmediate(_ship.GameObject);
            Object.DestroyImmediate(_config);
        }

        [Test]
        public void FieldOff_SpawnsNothingByItself()
        {
            var hazards = UseCase(fieldEnabled: false);

            for (int i = 0; i < 20; i++) hazards.Tick();

            Assert.That(_spawner.Spawned, Is.Empty);
        }

        [Test]
        public void FieldOn_FillsUpToMaxAlive_OnePerInterval()
        {
            var hazards = UseCase(fieldEnabled: true);

            hazards.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(1));
            hazards.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(1), "waits for the interval");

            for (int i = 0; i < 20; i++) hazards.Tick();
            Assert.That(_spawner.Spawned, Has.Count.EqualTo(3));
            Assert.That(hazards.Alive, Has.Count.EqualTo(3));
        }

        [Test]
        public void ShotDown_IsCounted_ThenDespawnedAfterTheDelay()
        {
            var hazards = UseCase(fieldEnabled: false);
            var hazard = (FakeHazard)hazards.SpawnAt(Vector3.zero)!;

            hazard.IsDestroyed = true;
            hazards.Tick();
            Assert.That(hazards.Destroyed, Is.EqualTo(1));
            Assert.That(_spawner.Despawned, Is.Empty, "kept for the delay");

            hazards.Tick();
            hazards.Tick();
            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { hazard }));
            Assert.That(hazards.Alive, Is.Empty);
            Assert.That(hazards.Destroyed, Is.EqualTo(1), "counted once");
        }

        [Test]
        public void FarBehind_IsRemoved_WhileTheFieldIsOn()
        {
            var hazards = UseCase(fieldEnabled: true);
            var far = hazards.SpawnAt(new Vector3(0f, 0f, -1000f))!;

            hazards.Tick();

            Assert.That(_spawner.Despawned, Has.Member(far));
            Assert.That(hazards.Alive, Has.No.Member(far));
        }

        [Test]
        public void SpawnPoint_IsAheadOfTheShip_LevelWithTheHorizon()
        {
            var shape = new SkyHazardFieldShape(new Vector3(-20f, 0f, 50f), new Vector3(20f, 10f, 100f));
            var pitchedAndTurned = Quaternion.Euler(-30f, 90f, 0f); // nose up 30 deg, heading +X

            Vector3 point = _field.SpawnPoint(new Vector3(0f, 100f, 0f), pitchedAndTurned, shape, lateral01: 0.5f, height01: 0f, ahead01: 0f);

            Assert.That(Vector3.Distance(point, new Vector3(50f, 100f, 0f)), Is.LessThan(1e-3f), "50 m along the heading, not up the pitched nose");
        }

        [Test]
        public void SpawnPoint_SpreadsWithinTheBox_OnTheStarboardSide()
        {
            var shape = new SkyHazardFieldShape(new Vector3(30f, -5f, -20f), new Vector3(80f, 10f, 100f));

            Vector3 low = _field.SpawnPoint(Vector3.zero, Quaternion.identity, shape, lateral01: 0f, height01: 0f, ahead01: 0f);
            Vector3 high = _field.SpawnPoint(Vector3.zero, Quaternion.identity, shape, lateral01: 1f, height01: 1f, ahead01: 1f);

            Assert.That(Vector3.Distance(low, new Vector3(30f, -5f, -20f)), Is.LessThan(1e-3f));
            Assert.That(Vector3.Distance(high, new Vector3(80f, 10f, 100f)), Is.LessThan(1e-3f));
        }

        [Test]
        public void TooFar_BeyondTheRemoveDistance()
        {
            Assert.That(_field.IsTooFar(Vector3.zero, new Vector3(0f, 0f, 99f), 100f), Is.False);
            Assert.That(_field.IsTooFar(Vector3.zero, new Vector3(0f, 0f, 101f), 100f), Is.True);
        }

        private SkyHazardUseCase UseCase(bool fieldEnabled)
        {
            _config.FieldEnabled = fieldEnabled;
            return new SkyHazardUseCase(new FakeNetworkService(), _actors, _time, new FakeEventPublisher(), _spawner, _field, _config, new System.Random(1));
        }
    }
}
