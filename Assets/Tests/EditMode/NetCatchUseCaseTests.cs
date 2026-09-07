#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Events;
using TinCan.Features.Abilities;
using TinCan.Features.Airship.Fuel;
using TinCan.Features.Airship.Fuel.Minigame;
using TinCan.Features.HumanoidMovement;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Covers the server-side catch: one catch per swing, only while the swinging tag is present, nearest can within
    /// reach, supply increment on the ship, and event publication.
    /// </summary>
    public class NetCatchUseCaseTests
    {
        private sealed class RecordingPublisher : IEventPublisher
        {
            public List<object> Events { get; } = new();
            public void Publish<TEvent>(TEvent evt) => Events.Add(evt!);
        }

        private FakeActorRegistry _registry = null!;
        private FakeFlyingCanSpawner _spawner = null!;
        private FlyingCanConfig _config = null!;
        private GameplayTag _swingTag = null!;
        private RecordingPublisher _events = null!;
        private NetCatchUseCase _useCase = null!;
        private FakeAirshipView _airship = null!;
        private FakeJerryCanSupplyBehaviour _supply = null!;
        private FakeHumanoidMovementView _movement = null!;
        private FakeNetHumanoidView _player = null!;
        private FakeTimeService _time = null!;
        private AbilitySystemUseCase _abilities = null!;
        private GameplayEffectDefinition _swingEffect = null!;

        [SetUp]
        public void SetUp()
        {
            _registry = new FakeActorRegistry();
            _spawner = new FakeFlyingCanSpawner(_registry);
            _config = ScriptableObject.CreateInstance<FlyingCanConfig>();
            _swingTag = ScriptableObject.CreateInstance<GameplayTag>();
            _config.SwingingTag = _swingTag;
            _config.NetReach = 2f;
            _config.NetHeight = 1f;
            _config.CatchRadius = 1.5f;
            _events = new RecordingPublisher();
            _time = new FakeTimeService();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), _registry, _time, _events);
            _swingEffect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _swingEffect.DurationType = DurationType.Duration;
            _swingEffect.DurationSeconds = 0.5f;
            _swingEffect.GrantedTags = new() { _swingTag };
            _swingEffect.Modifiers = new();
            _useCase = new NetCatchUseCase(new FakeNetworkService(), _registry, _spawner, new CatchProcessor(), _config, _events, _abilities);

            _airship = new FakeAirshipView("Airship");
            _supply = FakeJerryCanSupplyBehaviour.AttachTo(_airship.GameObject, 1);
            _registry.Register(_airship);

            _movement = new FakeHumanoidMovementView("Player");
            _movement.Transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // facing +Z
            _player = new FakeNetHumanoidView(_movement);
            _registry.Register(_player);
        }

        [TearDown]
        public void TearDown()
        {
            _spawner.DestroyAll();
            _movement.Destroy();
            _airship.Destroy();
            Object.DestroyImmediate(_config);
            Object.DestroyImmediate(_swingTag);
            Object.DestroyImmediate(_swingEffect);
        }

        [Test]
        public void Tick_SwingingNearACan_CatchesItOnce()
        {
            var inReach = _spawner.Spawn(new Vector3(0f, 1f, 2.5f))!; // net head is at (0,1,2)
            _spawner.Spawn(new Vector3(0f, 1f, 3f));
            _abilities.ApplyEffect(_player, _swingEffect);

            _useCase.Tick();
            _useCase.Tick(); // same swing, must not catch another can even though it is in reach

            Assert.That(_spawner.Despawned, Is.EqualTo(new[] { inReach }));
            Assert.That(_supply.Count, Is.EqualTo(2));
            Assert.That(_events.Events, Has.Exactly(1).TypeOf<JerryCanCaughtEvent>());
        }

        [Test]
        public void Tick_NotSwinging_NeverCatches()
        {
            _spawner.Spawn(new Vector3(0f, 1f, 2f));

            _useCase.Tick();

            Assert.That(_spawner.Despawned, Is.Empty);
            Assert.That(_supply.Count, Is.EqualTo(1));
        }

        [Test]
        public void Tick_NewSwingAfterTagDrops_CanCatchAgain()
        {
            _spawner.Spawn(new Vector3(0f, 1f, 2f));
            _abilities.ApplyEffect(_player, _swingEffect);
            _useCase.Tick();
            Assert.That(_spawner.Despawned.Count, Is.EqualTo(1));

            _time.Time = 0.5f;
            _abilities.ProcessAbilitySimulation(_player, default, 0, _time.DeltaTime);
            _useCase.Tick();
            _spawner.Spawn(new Vector3(0f, 1f, 2f));
            _abilities.ApplyEffect(_player, _swingEffect);
            _useCase.Tick();

            Assert.That(_spawner.Despawned.Count, Is.EqualTo(2));
            Assert.That(_supply.Count, Is.EqualTo(3));
        }

        [Test]
        public void Tick_SwingExpiresAndRestartsInSameSimulationStep_CatchesAgain()
        {
            var ability = Object.Instantiate(UnityEditor.AssetDatabase.LoadAssetAtPath<AbilityDefinition>(
                "Assets/Abilities/AbilityDefinitions/GA_SwingNet.asset"));
            var input = Object.Instantiate(ability.TriggerInput);
            input.BitIndex = 0;
            ability.TriggerInput = input;
            try
            {
                ability.ActiveEffect = _swingEffect;
                foreach (var tag in ability.ActivationRequiredTagsOnActor) _player.AddTag(tag);
                _abilities.GrantAbility(_player, ability);
                var pressed = new HumanoidInputState { ActiveInputMask = 1UL << ability.TriggerInput.BitIndex };
                _spawner.Spawn(new Vector3(0f, 1f, 2f));
                _abilities.ProcessAbilitySimulation(_player, pressed, 0, _time.DeltaTime);
                _useCase.Tick();

                _time.Time = 0.25f;
                _abilities.ProcessAbilitySimulation(_player, default, pressed.ActiveInputMask, _time.DeltaTime);
                _useCase.Tick();
                _spawner.Spawn(new Vector3(0f, 1f, 2f));
                _time.Time = 0.5f;
                _abilities.ProcessAbilitySimulation(_player, pressed, 0, _time.DeltaTime);
                Assert.That(_player.HasTag(_swingTag), Is.True);

                _useCase.Tick();

                Assert.That(_spawner.Despawned.Count, Is.EqualTo(2));
                Assert.That(_supply.Count, Is.EqualTo(3));
            }
            finally
            {
                Object.DestroyImmediate(input);
                Object.DestroyImmediate(ability);
            }
        }

        [Test]
        public void Tick_CanOutOfReach_NothingHappens()
        {
            _spawner.Spawn(new Vector3(4f, 1f, 2f));
            _abilities.ApplyEffect(_player, _swingEffect);

            _useCase.Tick();

            Assert.That(_spawner.Despawned, Is.Empty);
        }

        [Test]
        public void Tick_ExpiredEffectWithStaleTag_DoesNotCatch()
        {
            _abilities.ApplyEffect(_player, _swingEffect);
            _time.Time = 0.5f; // GAS has not removed the tag yet.
            _spawner.Spawn(new Vector3(0f, 1f, 2f));

            _useCase.Tick();

            Assert.That(_spawner.Despawned, Is.Empty);
        }

        [Test]
        public void Tick_TwoPlayersInReachOfOneCan_CatchesItOnlyOnce()
        {
            var otherPlayer = new FakeNetHumanoidView(_movement);
            _registry.Register(otherPlayer);
            _abilities.ApplyEffect(_player, _swingEffect);
            _abilities.ApplyEffect(otherPlayer, _swingEffect);
            _spawner.Spawn(new Vector3(0f, 1f, 2f));

            _useCase.Tick();

            Assert.That(_spawner.Despawned.Count, Is.EqualTo(1));
            Assert.That(_supply.Count, Is.EqualTo(2));
        }

        [Test]
        public void Tick_NoSwingTagConfigured_IsANoOp()
        {
            _config.SwingingTag = null;
            _spawner.Spawn(new Vector3(0f, 1f, 2f));
            _player.AddTag(_swingTag);

            Assert.DoesNotThrow(() => _useCase.Tick());
            Assert.That(_spawner.Despawned, Is.Empty);
        }
    }
}

