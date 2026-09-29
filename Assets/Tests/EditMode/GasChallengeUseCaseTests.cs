#nullable enable
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
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
        private FakeActorRegistry _actors = null!;
        private FakeGasPocketQuery _query = null!;
        private GasChallengeUseCase _useCase = null!;
        private GameplayTag _exploded = null!;

        [SetUp]
        public void SetUp()
        {
            _actors = new FakeActorRegistry();
            _query = new FakeGasPocketQuery();
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, new FakeTimeService(), new FakeEventPublisher());
            _useCase = new GasChallengeUseCase(_actors, new FakeNetworkService(), abilities, _query);
            _exploded = Track(ScriptableObject.CreateInstance<GameplayTag>());
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var instance in _objects) if (instance != null) Object.DestroyImmediate(instance);
            _objects.Clear();
        }

        [Test]
        public void TouchingPocket_DetonatesOnce_OnTheShip()
        {
            var ship = Ship();
            var pocket = Pocket();
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
            var pocket = Pocket();
            _query.Touching[idle] = new List<GasPocketVolume> { pocket };
            Ship(); // touches nothing

            _useCase.Tick();

            Assert.That(pocket.HasDetonated, Is.False);
        }

        private FakeAirshipView Ship()
        {
            var ship = new FakeAirshipView(controller: new FakeAbilityController());
            _actors.Register(ship);
            return ship;
        }

        private GasPocketVolume Pocket()
        {
            var effect = Track(ScriptableObject.CreateInstance<GameplayEffectDefinition>());
            effect.DurationType = DurationType.Infinite;
            effect.Modifiers = new List<AttributeModifier>();
            effect.GrantedTags = new List<GameplayTag> { _exploded };

            var pocket = Track(new GameObject("Pocket")).AddComponent<GasPocketVolume>();
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
