#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Tests.EditMode.Fakes;
using UnityEditor;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    /// <summary>
    /// One-shot abilities and the cancel/block tag lists (plan <c>cannon-and-hazards.md</c>, S1 GAS).
    /// A non-toggleable ability that is still active refuses a second activation, so a repeat activation shows
    /// whether the first one ended.
    /// </summary>
    public class AbilityActivationRulesTests
    {
        private readonly List<Object> _assets = new();
        private FakeTimeService _time = null!;
        private AbilitySystemUseCase _abilities = null!;
        private FakeAbilityController _actor = null!;
        private GameplayTag _abilityParent = null!;

        [SetUp]
        public void SetUp()
        {
            _time = new FakeTimeService { Tick = 0 };
            _abilities = new AbilitySystemUseCase(new FakeAbilityRegistry(), new FakeActorRegistry(), _time, new FakeEventPublisher());
            _actor = new FakeAbilityController();
            _abilityParent = Create<GameplayTag>("Ability");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _assets) Object.DestroyImmediate(asset);
            _assets.Clear();
        }

        [Test]
        public void EndsImmediately_EndsInTheTickItActivates()
        {
            var shot = Grant(Ability("GA_Shot"));
            shot.EndsImmediately = true;

            Assert.That(_abilities.TryActivateAbility(_actor, shot), Is.True);
            Assert.That(_abilities.TryActivateAbility(_actor, shot), Is.True, "already ended, so it can fire again");
        }

        [Test]
        public void EndsImmediately_KeepsItsCooldown()
        {
            var shot = Grant(Ability("GA_Shot"));
            shot.EndsImmediately = true;
            shot.CooldownEffect = Cooldown(1f);

            Assert.That(_abilities.TryActivateAbility(_actor, shot), Is.True);
            _time.Tick = 29;
            Assert.That(_abilities.TryActivateAbility(_actor, shot), Is.False, "reloading");
            _time.Tick = 30;
            Assert.That(_abilities.TryActivateAbility(_actor, shot), Is.True);
        }

        [Test]
        public void WithoutEndsImmediately_StaysActive()
        {
            var held = Grant(Ability("GA_Held"));

            Assert.That(_abilities.TryActivateAbility(_actor, held), Is.True);
            Assert.That(_abilities.TryActivateAbility(_actor, held), Is.False);
        }

        [Test]
        public void CancelAbilitiesWithTag_EndsMatchingActiveAbilities_ByParentTag()
        {
            var sprint = Grant(Ability("GA_Sprint", Tag("Ability.Sprint")));
            var occupy = Grant(Ability("GA_Occupy", Tag("Ability.Occupy")));
            occupy.CancelAbilitiesWithTag.Add(_abilityParent);
            Assert.That(_abilities.TryActivateAbility(_actor, sprint), Is.True);

            Assert.That(_abilities.TryActivateAbility(_actor, occupy), Is.True);

            Assert.That(_abilities.TryActivateAbility(_actor, sprint), Is.True, "sprint was cancelled");
            Assert.That(_abilities.TryActivateAbility(_actor, occupy), Is.False, "the canceller does not cancel itself");
        }

        [Test]
        public void CancelAbilitiesWithTag_LeavesOtherTagsAlone()
        {
            var sprint = Grant(Ability("GA_Sprint", Tag("Ability.Sprint")));
            var occupy = Grant(Ability("GA_Occupy", Tag("Ability.Occupy")));
            occupy.CancelAbilitiesWithTag.Add(Tag("Ability.Swing"));
            _abilities.TryActivateAbility(_actor, sprint);

            _abilities.TryActivateAbility(_actor, occupy);

            Assert.That(_abilities.TryActivateAbility(_actor, sprint), Is.False, "sprint is still active");
        }

        [Test]
        public void BlockAbilitiesWithTag_BlocksWhileActive_ThenReleases()
        {
            var occupy = Grant(Ability("GA_Occupy", Tag("Ability.Occupy")));
            var sprint = Grant(Ability("GA_Sprint", Tag("Ability.Sprint")));
            occupy.BlockAbilitiesWithTag.Add(_abilityParent);

            _abilities.TryActivateAbility(_actor, occupy);
            Assert.That(_abilities.TryActivateAbility(_actor, sprint), Is.False, "blocked while occupying");

            _abilities.CancelAbility(_actor, occupy);
            Assert.That(_abilities.TryActivateAbility(_actor, sprint), Is.True);
        }

        [Test]
        public void BlockAbilitiesWithTag_DoesNotBlockItself()
        {
            var occupy = Grant(Ability("GA_Occupy", Tag("Ability.Occupy")));
            occupy.IsToggleable = true;
            occupy.BlockAbilitiesWithTag.Add(_abilityParent);

            _abilities.TryActivateAbility(_actor, occupy);

            Assert.That(_abilities.TryActivateAbility(_actor, occupy), Is.True, "toggling itself off is not blocked");
        }

        private AbilityDefinition Grant(AbilityDefinition ability)
        {
            _abilities.GrantAbility(_actor, ability);
            return ability;
        }

        private AbilityDefinition Ability(string name, GameplayTag? tag = null)
        {
            var ability = Create<AbilityDefinition>(name);
            ability.AbilityTag = tag!;
            ability.CancelAbilitiesWithTag = new List<GameplayTag>();
            ability.BlockAbilitiesWithTag = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnActor = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnActor = new List<GameplayTag>();
            ability.ActivationRequiredTagsOnTarget = new List<GameplayTag>();
            ability.ActivationBlockedTagsOnTarget = new List<GameplayTag>();
            ability.TimingTagWindows = new List<AbilityTagWindow>();
            return ability;
        }

        private GameplayEffectDefinition Cooldown(float seconds)
        {
            var effect = Create<GameplayEffectDefinition>("GE_Cooldown");
            effect.DurationType = DurationType.Duration;
            effect.DurationSeconds = seconds;
            effect.Modifiers = new List<AttributeModifier>();
            effect.GrantedTags = new List<GameplayTag>();
            return effect;
        }

        private GameplayTag Tag(string name)
        {
            var tag = Create<GameplayTag>(name);
            var serialized = new SerializedObject(tag);
            serialized.FindProperty("_parent").objectReferenceValue = _abilityParent;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return tag;
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
