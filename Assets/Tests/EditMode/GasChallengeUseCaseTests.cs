#nullable enable
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;
using TinCan.Features.GasChallenge;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    public class GasChallengeUseCaseTests
    {
        private sealed class FakeGasPocketQuery : IGasPocketQuery
        {
            public readonly Dictionary<IAirshipView, List<GasPocketVolume>> Touching = new();

            void IGasPocketQuery.Touching(IAirshipView ship, List<GasPocketVolume> results)
            {
                results.Clear();
                if (Touching.TryGetValue(ship, out var pockets)) results.AddRange(pockets);
            }
        }

        private readonly List<Object> _objects = new();
        private readonly List<FakeAirshipView> _ships = new();
        private FakeActorRegistry _actors = null!;
        private FakeGasPocketQuery _query = null!;
        private FakeShipBreakage _breakage = null!;
        private GasChallengeConfig _config = null!;
        private GasChallengeUseCase _useCase = null!;
        private GameplayTag _exploded = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _query = new FakeGasPocketQuery();
            _breakage = new FakeShipBreakage();
            _config = Track(ScriptableObject.CreateInstance<GasChallengeConfig>());
            _config.BlastRadius = 5f;
            _config.MaxBreaks = 2;
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, new FakeTimeService(), new FakeEventPublisher());
            _useCase = new GasChallengeUseCase(_actors, new FakeNetworkService(), abilities, _query, _breakage, new GasBlastProcessor(), _config,
                new FakeEventPublisher());
            _exploded = Track(ScriptableObject.CreateInstance<GameplayTag>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var ship in _ships) ship.Destroy();
            _ships.Clear();
            foreach (var instance in _objects) if (instance != null) Object.DestroyImmediate(instance);
            _objects.Clear();
        }

        [Test]
        public void TouchingPocket_DetonatesOnce_OnTheShip()
        {
            var ship = Ship();
            var pocket = Pocket(Vector3.zero);
            _query.Touching[ship] = new List<GasPocketVolume> { pocket };

            _useCase.Tick();
            _useCase.Tick();

            Assert.That(pocket.HasDetonated, Is.True);
            Assert.That(ship.Controller.HasTag(_exploded), Is.True, "the explosion effect landed on the ship");
            Assert.That(_useCase.Phase, Is.EqualTo(SimulationPhase.AfterAirship));
        }

        [Test]
        public void ShipNotTouching_OrNotSimulating_LeavesPocketsAlone()
        {
            var idle = Ship();
            idle.IsSimulating = false;
            var pocket = Pocket(Vector3.zero);
            _query.Touching[idle] = new List<GasPocketVolume> { pocket };
            Ship(); // touches nothing

            _useCase.Tick();

            Assert.That(pocket.HasDetonated, Is.False);
        }

        [Test]
        public void Blast_BreaksTheFittingNearest_BeforeHullSpots_WithinRadius_Once()
        {
            var ship = Ship();
            var points = FakeShipDamage.AttachPoints(ship.GameObject, 4, Track(ScriptableObject.CreateInstance<HealthAttribute>()),
                Track(ScriptableObject.CreateInstance<MaxHealthAttribute>()));
            points[0].transform.position = new Vector3(1f, 0f, 0f);  // hull, nearest
            points[1].transform.position = new Vector3(3f, 0f, 0f);  // fitting
            points[2].transform.position = new Vector3(4f, 0f, 0f);  // fitting
            points[3].transform.position = new Vector3(20f, 0f, 0f); // fitting, out of reach
            for (int i = 1; i < 4; i++) points[i].ServerAssign(i, new ShipSocketId(10 + i, 0));
            var pocket = Pocket(Vector3.zero);
            _query.Touching[ship] = new List<GasPocketVolume> { pocket };

            _useCase.Tick();
            _useCase.Tick();

            Assert.That(_breakage.Broken, Is.EquivalentTo(new[] { 1, 2 }), "the two fittings in reach; the nearer hull spot waits");
        }

        private FakeAirshipView Ship()
        {
            var ship = new FakeAirshipView(controller: new FakeAbilityController());
            _ships.Add(ship);
            _actors.Register(ship);
            return ship;
        }

        private GasPocketVolume Pocket(Vector3 at)
        {
            var effect = Track(ScriptableObject.CreateInstance<GameplayEffectDefinition>());
            effect.DurationType = DurationType.Infinite;
            effect.Modifiers = new List<AttributeModifier>();
            effect.GrantedTags = new List<GameplayTag> { _exploded };

            var pocket = Track(new GameObject("Pocket")).AddComponent<GasPocketVolume>();
            pocket.transform.position = at;
            typeof(GasPocketVolume).GetField("_explosionEffect", BindingFlags.NonPublic | BindingFlags.Instance)!.SetValue(pocket, effect);
            return pocket;
        }

        private T Track<T>(T instance) where T : Object
        {
            _objects.Add(instance);
            return instance;
        }
    }
}
