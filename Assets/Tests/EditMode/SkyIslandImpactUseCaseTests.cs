#nullable enable
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Core.Gas;
using TinCan.Core.Ship;
using TinCan.Features.SkyIslands;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// <see cref="SkyIslandImpactUseCase"/>: a ship in island rock is pushed out every tick, and hurt at most once per
    /// cooldown; nothing happens off the server or away from rock.
    /// </summary>
    public class SkyIslandImpactUseCaseTests
    {
        private sealed class FakeContact : ISkyIslandContactQuery
        {
            public Vector3? Push;

            public bool TryPushOut(IAirshipView ship, out Vector3 push, out SkyIslandId island)
            {
                push = Push ?? Vector3.zero;
                island = new SkyIslandId(1, 2, 3, 0);
                return Push != null;
            }
        }

        private sealed class RecordingResponse : IAirshipCollisionResponse
        {
            public readonly List<Vector3> Pushes = new();
            public void Push(IAirshipView airship, Vector3 push) => Pushes.Add(push);
        }

        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private readonly List<ScriptableObject> _assets = new();
        private FakeTimeService _time = null!;
        private FakeActorRegistry _actors = null!;
        private RecordingPublisher _events = null!;
        private FakeContact _contact = null!;
        private RecordingResponse _response = null!;
        private SkyIslandConfig _config = null!;
        private HealthAttribute _health = null!;
        private FakeAbilityController _shipController = null!;
        private FakeAirshipView _ship = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService { DeltaTime = 0.5f };
            _actors = new FakeActorRegistry();
            _events = new RecordingPublisher();
            _contact = new FakeContact();
            _response = new RecordingResponse();
            _config = Create<SkyIslandConfig>("SkyIslandConfig");
            _config.ImpactCooldown = 1.5f;

            _health = Create<HealthAttribute>("Attr_Health");
            var maxHealth = Create<MaxHealthAttribute>("Attr_MaxHealth");
            _config.ImpactEffect = Create<GameplayEffectDefinition>("GE_IslandImpact");
            _config.ImpactEffect.DurationType = DurationType.Instant;
            _config.ImpactEffect.GrantedTags = new List<GameplayTag>();
            _config.ImpactEffect.Modifiers = new List<AttributeModifier>
            {
                new() { Attribute = _health, Operation = ModifierOp.Add, Value = -80f, ClampMaxAttribute = maxHealth }
            };

            _shipController = new FakeAbilityController();
            _shipController.SetAttribute(maxHealth, new AttributeValue(1000f));
            _shipController.SetAttribute(_health, new AttributeValue(1000f));
            _ship = new FakeAirshipView("Airship", _shipController);
            _actors.Register(_ship);
        }

        [TearDown]
        public void TearDown()
        {
            _ship.Destroy();
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        private float ShipHealth => _shipController.TryGetAttribute(_health, out var value) ? value.CurrentValue : -1f;

        [Test]
        public void AwayFromRock_NothingHappens()
        {
            var impact = UseCase();

            impact.Tick();

            Assert.That(_response.Pushes, Is.Empty);
            Assert.That(ShipHealth, Is.EqualTo(1000f));
        }

        [Test]
        public void InRock_TheShipIsPushedOut_AndHurtOnce()
        {
            _contact.Push = new Vector3(0f, 0f, -0.4f);
            var impact = UseCase();

            impact.Tick();

            Assert.That(_response.Pushes, Is.EqualTo(new[] { new Vector3(0f, 0f, -0.4f) }));
            Assert.That(ShipHealth, Is.EqualTo(920f).Within(1e-3f));
            Assert.That(impact.Hits, Is.EqualTo(1));
            var hit = _events.Events.OfType<SkyIslandHitShipEvent>().Single();
            Assert.That(hit.ShipId, Is.EqualTo(_ship.Id));
            Assert.That(hit.Island, Is.EqualTo(new SkyIslandId(1, 2, 3, 0)));
        }

        [Test]
        public void ScrapingAlong_IsPushedEveryTick_ButHurtOncePerCooldown()
        {
            _contact.Push = new Vector3(0.1f, 0f, 0f);
            var impact = UseCase();

            for (int tick = 0; tick < 6; tick++) impact.Tick(); // 3 s at 0.5 s a tick

            Assert.That(_response.Pushes.Count, Is.EqualTo(6));
            Assert.That(impact.Hits, Is.EqualTo(2), "at 0 s and after the 1.5 s cooldown");
            Assert.That(ShipHealth, Is.EqualTo(840f).Within(1e-3f));
        }

        [Test]
        public void AShipTheServerDoesNotSimulate_IsLeftAlone()
        {
            _contact.Push = new Vector3(0f, 1f, 0f);
            _ship.IsSimulating = false;

            UseCase().Tick();

            Assert.That(_response.Pushes, Is.Empty);
        }

        [Test]
        public void OnAClient_NothingHappens()
        {
            _contact.Push = new Vector3(0f, 1f, 0f);
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);
            var impact = new SkyIslandImpactUseCase(new FakeSessionNetworkService { IsClient = true }, _actors, _time, _events, _contact, _response, abilities, _config);

            impact.Tick();

            Assert.That(_response.Pushes, Is.Empty);
            Assert.That(ShipHealth, Is.EqualTo(1000f));
        }

        private SkyIslandImpactUseCase UseCase()
        {
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _actors, _time, _events);
            return new SkyIslandImpactUseCase(new FakeNetworkService(), _actors, _time, _events, _contact, _response, abilities, _config);
        }

        private T Create<T>(string name) where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            asset.name = name;
            _assets.Add(asset);
            return asset;
        }
    }
}
