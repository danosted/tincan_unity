#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Tests.EditMode.Fakes;
using UnityEditor;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// GAS counts time in simulation ticks, has one controller per actor, and matches tags the same way on every peer
    /// (review A1, A4; plan <c>gas-core-correctness.md</c>).
    /// </summary>
    public class GasTickTimingTests
    {
        private sealed class SimulatedController : FakeAbilityController, ISimulatedActor { }

        private readonly List<Object> _assets = new();
        private GameplayTag _tag = null!;

        [SetUp]
        public void SetUp() => _tag = Create<GameplayTag>("State.Test");

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [TestCase(0f, 0)]
        [TestCase(1f / 30f, 1)]
        [TestCase(0.5f, 15)]
        [TestCase(0.51f, 16)]
        public void FromSeconds_RoundsUpToWholeTicks(float seconds, int ticks) =>
            Assert.That(GameplayTicks.FromSeconds(seconds, 30), Is.EqualTo(ticks));

        [Test]
        public void DurationEffect_ExpiresOnItsTick_WhateverTheStartTick()
        {
            var definition = Effect(0.5f);

            // Owner and server apply the same input on different absolute ticks; both windows last 15 ticks.
            foreach (int start in new[] { 40, 1000 })
            {
                var effect = new ActiveGameplayEffect(definition, start, 30);
                Assert.That(effect.IsExpired(start + 14), Is.False);
                Assert.That(effect.IsExpired(start + 15), Is.True);
            }
        }

        [Test]
        public void Cooldown_NotActiveBeforeFirstActivation_ThenLastsItsTicks()
        {
            var ability = Ability();
            ability.CooldownEffect = Effect(1f);
            var spec = new AbilitySpec(ability);

            Assert.That(spec.IsOnCooldown(0, 30), Is.False, "tick 0 is not a cooldown");

            spec.Activate(10);
            Assert.That(spec.IsOnCooldown(39, 30), Is.True);
            Assert.That(spec.IsOnCooldown(40, 30), Is.False);
        }

        [Test]
        public void GlobalTick_ExpiresPlainControllers_AndLeavesSimulatedOnesToTheirLoop()
        {
            var time = new FakeTimeService { Tick = 100 };
            var registry = new FakeAbilityRegistry();
            var abilities = new AbilitySystemUseCase(registry, new FakeActorRegistry(), time, new FakeEventPublisher());
            var ship = new FakeAbilityController();
            var player = new SimulatedController();
            registry.Register(ship);
            registry.Register(player);
            abilities.ApplyEffect(ship, Effect(0.5f));
            abilities.ApplyEffect(player, Effect(0.5f));

            time.Tick = 115;
            abilities.Tick();

            Assert.That(abilities.Phase, Is.EqualTo(SimulationPhase.AfterHumanoid));
            Assert.That(ship.HasTag(_tag), Is.False, "the global loop expires a plain controller");
            Assert.That(player.HasTag(_tag), Is.True, "a simulated actor is not ticked by the global loop");

            abilities.ProcessAbilitySimulation(player, default, 0, time.DeltaTime);
            Assert.That(player.HasTag(_tag), Is.False, "its own loop expires it");
        }

        [Test]
        public void TimingWindow_OpensAndClosesOnTicks()
        {
            var time = new FakeTimeService { Tick = 0 };
            var abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), new FakeActorRegistry(), time, new FakeEventPublisher());
            var actor = new SimulatedController();
            var ability = Ability();
            ability.TimingTagWindows.Add(new AbilityTagWindow { Tag = _tag, StartOffset = 0.1f, Duration = 0.2f }); // ticks 3..9
            abilities.GrantAbility(actor, ability);
            Assert.That(abilities.TryActivateAbility(actor, ability), Is.True);

            var held = new List<int>();
            for (int tick = 0; tick <= 12; tick++)
            {
                time.Tick = tick;
                abilities.ProcessAbilitySimulation(actor, default, 0, time.DeltaTime);
                if (actor.HasTag(_tag)) held.Add(tick);
            }

            Assert.That(held, Is.EqualTo(new[] { 3, 4, 5, 6, 7, 8, 9 }));
        }

        [Test]
        public void OnePerActor_PrefersTheSimulatedController_ForASharedId()
        {
            var player = new SimulatedController();
            var mediator = new FakeAbilityController { Id = player.Id };
            var ship = new FakeAbilityController();

            var chosen = AbilityControllerSelection.OnePerActor(new IAbilityControllerBase[] { mediator, ship, player });

            Assert.That(chosen, Is.EqualTo(new IAbilityControllerBase[] { player, ship }));
        }

        [Test]
        public void ClientTags_ParentQueryMatchesHeldChild_LikeTheServer()
        {
            var parent = Create<GameplayTag>("State.Carrying");
            var child = Create<GameplayTag>("State.Carrying.Net");
            SetParent(child, parent);
            var registry = new GameplayTagRegistry(new[] { parent, child });
            var server = new GameplayTagContainer(new[] { child });
            var client = new ClientTagState();
            client.SetReplicated(new[] { child.name });

            Assert.That(server.HasTag(parent), Is.True);
            Assert.That(client.Has(parent, registry), Is.EqualTo(server.HasTag(parent)));
            Assert.That(client.Has(parent, null), Is.False, "without the registry only exact names match");

            client.SetReplicated(new string[0]);
            Assert.That(client.Has(parent, registry), Is.False, "the server removed the child");

            client.AddPredicted(child.name);
            Assert.That(client.Has(parent, registry), Is.True, "a predicted child matches too");
            Assert.That(client.Has(child, registry), Is.True);
            Assert.That(new ClientTagState().Has(child, registry), Is.False);
        }

        private GameplayEffectDefinition Effect(float seconds)
        {
            var effect = Create<GameplayEffectDefinition>("GE_Test");
            effect.DurationType = DurationType.Duration;
            effect.DurationSeconds = seconds;
            effect.Modifiers = new List<AttributeModifier>();
            effect.GrantedTags = new List<GameplayTag> { _tag };
            return effect;
        }

        private AbilityDefinition Ability()
        {
            var ability = Create<AbilityDefinition>("GA_Test");
            ability.ActivationRequiredTagsOnActor = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnActor = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnTarget = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnTarget = new List<GameplayTag>();
            ability.TimingTagWindows = new List<AbilityTagWindow>();
            return ability;
        }

        private static void SetParent(GameplayTag tag, GameplayTag parent)
        {
            var serialized = new SerializedObject(tag);
            serialized.FindProperty("_parent").objectReferenceValue = parent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
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
