#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Features.Abilities.Cues;
using TinCan.Features.Abilities.Inputs;
using TinCan.Features.HumanoidMovement;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// Effects declare cues; the duration type decides the kind. Instant effects dispatch bursts; Duration/Infinite
    /// effects put their cue tags on the actor while active. Only the input-driven simulation marks a cue predicted.
    /// </summary>
    public class AbilitySystemCueTests
    {
        private readonly List<Object> _assets = new();
        private FakeTimeService _time = null!;
        private RecordingCueDispatcher _dispatcher = null!;
        private AbilitySystemUseCase _abilities = null!;
        private FakeAbilityController _actor = null!;
        private GameplayTag _bang = null!;
        private GameplayTag _broken = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService();
            _dispatcher = new RecordingCueDispatcher();
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), new FakeActorRegistry(), _time, new FakeEventPublisher(), _dispatcher);
            _actor = new FakeAbilityController();
            _bang = Create<GameplayTag>("Cue.Test.Bang");
            _broken = Create<GameplayTag>("Cue.Test.Broken");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void InstantEffect_ExecutesEachCue_NotPredicted()
        {
            _abilities.ApplyEffect(_actor, Effect(DurationType.Instant, _bang, _broken));

            Assert.That(_dispatcher.Executed, Has.Count.EqualTo(2));
            Assert.That(_dispatcher.Executed[0].Cue, Is.SameAs(_bang));
            Assert.That(_dispatcher.Executed[0].Target, Is.SameAs(_actor));
            Assert.That(_dispatcher.Executed[0].Context.IsPredicted, Is.False);
            Assert.That(_actor.HasTag(_bang), Is.False, "a burst leaves no tag behind");
        }

        [Test]
        public void InfiniteEffect_HoldsItsCueTag_UntilRemoved_WithoutBursts()
        {
            var handle = _abilities.ApplyEffect(_actor, Effect(DurationType.Infinite, _broken));
            Assert.That(_actor.HasTag(_broken), Is.True);
            Assert.That(_dispatcher.Executed, Is.Empty);

            _abilities.RemoveEffect(_actor, handle);
            Assert.That(_actor.HasTag(_broken), Is.False);
        }

        [Test]
        public void SharedCueTag_StaysWhileAnotherEffectHoldsIt()
        {
            var first = _abilities.ApplyEffect(_actor, Effect(DurationType.Infinite, _broken));
            var second = _abilities.ApplyEffect(_actor, Effect(DurationType.Infinite, _broken));

            _abilities.RemoveEffect(_actor, first);
            Assert.That(_actor.HasTag(_broken), Is.True, "the second effect still holds the cue");

            _abilities.RemoveEffect(_actor, second);
            Assert.That(_actor.HasTag(_broken), Is.False);
        }

        [Test]
        public void DurationEffect_CueTagExpiresWithIt()
        {
            var effect = Effect(DurationType.Duration, _broken);
            effect.DurationSeconds = 1f;
            _abilities.ApplyEffect(_actor, effect);
            Assert.That(_actor.HasTag(_broken), Is.True);

            _time.Time = 1.5f;
            _abilities.ProcessAbilitySimulation(_actor, default, 0, _time.DeltaTime);

            Assert.That(_actor.HasTag(_broken), Is.False);
        }

        [Test]
        public void NullCueEntries_AreSkipped()
        {
            var effect = Effect(DurationType.Infinite);
            effect.Cues.Add(null!);

            Assert.DoesNotThrow(() => _abilities.ApplyEffect(_actor, effect));
            effect.DurationType = DurationType.Instant;
            Assert.DoesNotThrow(() => _abilities.ApplyEffect(_actor, effect));
            Assert.That(_dispatcher.Executed, Is.Empty);
        }

        [Test]
        public void InputDrivenActivation_MarksCuesPredicted()
        {
            var ability = InputAbility(Effect(DurationType.Instant, _bang));
            _abilities.GrantAbility(_actor, ability);

            _abilities.ProcessAbilitySimulation(_actor, new HumanoidInputState { ActiveInputMask = 1 }, 0, _time.DeltaTime);

            Assert.That(_dispatcher.Executed, Has.Count.EqualTo(1));
            Assert.That(_dispatcher.Executed[0].Context.IsPredicted, Is.True);
        }

        [Test]
        public void DirectActivation_IsNotPredicted()
        {
            var ability = InputAbility(Effect(DurationType.Instant, _bang));
            _abilities.GrantAbility(_actor, ability);

            _abilities.TryActivateAbility(_actor, ability);

            Assert.That(_dispatcher.Executed, Has.Count.EqualTo(1));
            Assert.That(_dispatcher.Executed[0].Context.IsPredicted, Is.False);
        }

        [Test]
        public void WithoutDispatcher_InstantCuesAreSilent()
        {
            var silent = new AbilitySystemUseCase(new FakeAbilityRegistry(), new FakeActorRegistry(), _time, new FakeEventPublisher());

            Assert.DoesNotThrow(() => silent.ApplyEffect(_actor, Effect(DurationType.Instant, _bang)));
        }

        private GameplayEffectDefinition Effect(DurationType duration, params GameplayTag[] cues)
        {
            var effect = Create<GameplayEffectDefinition>("GE_Test");
            effect.DurationType = duration;
            effect.Modifiers = new List<AttributeModifier>();
            effect.GrantedTags = new List<GameplayTag>();
            effect.Cues = new List<GameplayTag>(cues);
            return effect;
        }

        private AbilityDefinition InputAbility(GameplayEffectDefinition activeEffect)
        {
            var input = Create<PrimaryInput>("Input_Test");
            input.BitIndex = 0;
            var ability = Create<AbilityDefinition>("GA_Test");
            ability.TriggerInput = input;
            ability.InputPolicy = AbilityInputPolicy.OnInputTriggered;
            ability.ActiveEffect = activeEffect;
            ability.ActivationRequiredTagsOnActor = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnActor = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnTarget = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnTarget = new List<GameplayTag>();
            ability.TimingTagWindows = new List<AbilityTagWindow>();
            return ability;
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
