#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Features.GasChallenge;
using UnityEngine;
using TinCan.Tests.Fakes;
using Object = UnityEngine.Object;

namespace TinCan.Features.GasChallenge.Tests
{
    /// <summary>
    /// The gas field: GasFieldProcessor (spots, passing), GasFieldUseCase (lay, clear, cleared) and its event handlers
    /// (SpawnGasFieldActionHandler, ClearGasFieldActionHandler, GasFieldClearedConditionHandler).
    /// </summary>
    public class GasFieldTests
    {
        private sealed class FixedRandom : IRandomSource
        {
            public int? Seed => 7;
            public System.Random Create(string stream) => new(7);
        }

        private sealed class BlockedBelow : IWorldObstacleQuery
        {
            public float Y;
            public bool IsBlocked(Vector3 point, float radius) => point.y < Y;
        }

        private readonly GasFieldProcessor _processor = new();
        private FakeActorRegistry _actors = null!;
        private FakeGasPockets _spawner = null!;
        private GasChallengeConfig _config = null!;
        private FakeAirshipView _ship = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _spawner = new FakeGasPockets();
            _config = ScriptableObject.CreateInstance<GasChallengeConfig>();
            _config.PassedDistance = 50f;
            _config.FieldDepth = 40f;
            _config.FieldHeight = 10f;
            _ship = new FakeAirshipView();
            _actors.Register(_ship);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            Object.DestroyImmediate(_config);
        }

        private GasFieldUseCase Field(params IWorldObstacleQuery[] obstacles) => new(new FakeNetworkService(), _actors, _spawner, _processor, _config,
            new FakeEventPublisher(), new FixedRandom(), obstacles);

        [Test]
        public void Spots_FillOneLaneEachAcrossTheBand_AheadAlongTheHeading()
        {
            var spots = _processor.Spots(Vector3.zero, Vector3.forward, 4, 150f, 40f, 40f, 10f, new System.Random(1), _ => false, 3);

            Assert.That(spots.Count, Is.EqualTo(4));
            for (int i = 0; i < 4; i++)
            {
                Assert.That(spots[i].x, Is.InRange(-40f + 20f * i, -40f + 20f * (i + 1)), $"lane {i}");
                Assert.That(spots[i].z, Is.InRange(130f, 170f));
                Assert.That(spots[i].y, Is.InRange(-5f, 5f));
            }
        }

        [Test]
        public void Spots_InRock_AreRetried_ThenDropped()
        {
            var always = _processor.Spots(Vector3.zero, Vector3.forward, 3, 100f, 20f, 0f, 0f, new System.Random(1), _ => true, 4);
            Assert.That(always, Is.Empty);

            int calls = 0;
            var once = _processor.Spots(Vector3.zero, Vector3.forward, 1, 100f, 20f, 0f, 0f, new System.Random(1), _ => calls++ == 0, 4);
            Assert.That(once.Count, Is.EqualTo(1), "moved on the second try");
        }

        [Test]
        public void Heading_FollowsMotion_ElseTheBow_AlwaysLevel()
        {
            Assert.That(_processor.Heading(new Vector3(0f, 5f, -10f), Vector3.forward), Is.EqualTo(Vector3.back));
            Assert.That(_processor.Heading(Vector3.zero, new Vector3(1f, 1f, 0f)), Is.EqualTo(Vector3.right));
        }

        [Test]
        public void IsPassed_OnlyBeyondThePassedDistanceBehind()
        {
            Assert.That(_processor.IsPassed(new Vector3(0f, 0f, -49f), Vector3.zero, Vector3.forward, 50f), Is.False);
            Assert.That(_processor.IsPassed(new Vector3(30f, 0f, -51f), Vector3.zero, Vector3.forward, 50f), Is.True);
        }

        [Test]
        public void Spawn_LaysTheFieldAhead_ReplacingAnOldOne()
        {
            var field = Field();

            Assert.That(field.Spawn(3, 150f, 40f), Is.EqualTo(3));
            var first = field.Pockets.ToList();
            Assert.That(first.All(pocket => pocket.Center.z > 100f), Is.True, "ahead of the bow (+z)");

            field.Spawn(2, 150f, 40f);
            Assert.That(_spawner.Despawned, Is.EquivalentTo(first));
            Assert.That(field.Pockets.Count, Is.EqualTo(2));
        }

        [Test]
        public void Spawn_SkipsRock()
        {
            var field = Field(new BlockedBelow { Y = 100f });
            Assert.That(field.Spawn(3, 150f, 40f), Is.Zero);
            Assert.That(field.IsCleared, Is.True, "no field: nothing to clear");
        }

        [Test]
        public void IsCleared_WhenEveryPocketWentOffOrWasLeftBehind()
        {
            var field = Field();
            field.Spawn(2, 150f, 40f);
            Assert.That(field.IsCleared, Is.False);

            _spawner.Spawned[0].Detonated = true;
            Assert.That(field.IsCleared, Is.False);

            _ship.Transform.position = new Vector3(0f, 0f, 400f); // flew past the second
            Assert.That(field.IsCleared, Is.True);
        }

        [Test]
        public void SessionEnd_AndClear_DespawnTheField()
        {
            var field = Field();
            field.Spawn(2, 150f, 40f);
            field.SetSessionActive(false);
            Assert.That(_spawner.Despawned.Count, Is.EqualTo(2));
            Assert.That(field.Pockets, Is.Empty);
        }

        [Test]
        public void GasSquall_ReplacesHullStressInTheRotation_FieldThenRepair_ClearsUpEitherWay()
        {
            var squall = GasChallengeDesignedEvents.GasSquall;

            Assert.That(squall.Supersedes, Is.EqualTo(new[] { TinCan.Features.Airship.Damage.ShipDamageDesignedEvents.HullStress.Id }));
            Assert.That(squall.Phases.Select(phase => phase.Name), Is.EqualTo(new[] { "Warn", "Field", "Repair" }));
            Assert.That(squall.SuccessActions.OfType<ClearGasField>().Count(), Is.EqualTo(1));
            Assert.That(squall.FailureActions.OfType<ClearGasField>().Count(), Is.EqualTo(1));
        }

        [Test]
        public void EventHandlers_SpawnClearAndAnswerCleared()
        {
            var field = Field();
            Assert.That(new SpawnGasFieldActionHandler(field).Execute(new SpawnGasField(2, 150f, 40f)), Is.True);
            Assert.That(field.Pockets.Count, Is.EqualTo(2));

            var cleared = new GasFieldClearedConditionHandler(field);
            Assert.That(cleared.IsMet(new GasFieldCleared()), Is.False);

            Assert.That(new ClearGasFieldActionHandler(field).Execute(new ClearGasField()), Is.True);
            Assert.That(field.Pockets, Is.Empty);
            Assert.That(cleared.IsMet(new GasFieldCleared()), Is.True);
        }
    }
}
