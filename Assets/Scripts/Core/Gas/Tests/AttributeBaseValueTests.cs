#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Tests.Fakes;
using UnityEngine;

namespace TinCan.Core.Gas.Tests
{
    /// <summary>
    /// <see cref="AbilitySystemUseCase.SetBaseValue"/>: a new base (a ship's handling tuned live) keeps the effects
    /// running on the attribute on top of it.
    /// </summary>
    public class AttributeBaseValueTests
    {
        private MoveSpeedAttribute _speed = null!;
        private GameplayEffectDefinition _boost = null!;
        private AbilitySystemUseCase _abilitySystem = null!;
        private FakeAbilityController _ship = null!;

        [SetUp]
        public void SetUp()
        {
            _speed = ScriptableObject.CreateInstance<MoveSpeedAttribute>();
            _speed.name = "Attr_Speed_Test";

            _boost = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _boost.DurationType = DurationType.Infinite;
            _boost.GrantedTags = new List<GameplayTag>();
            _boost.Modifiers = new List<AttributeModifier>
            {
                new AttributeModifier { Attribute = _speed, Operation = ModifierOp.Multiply, Value = 2f }
            };

            _abilitySystem = new AbilitySystemUseCase(
                new FakeAbilityRegistry(),
                new FakeActorRegistry(),
                new FakeTimeService(),
                new FakeEventPublisher());

            _ship = new FakeAbilityController();
            _ship.SetAttribute(_speed, new AttributeValue(10f));
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_speed);
            Object.DestroyImmediate(_boost);
        }

        [Test]
        public void ANewBase_KeepsTheBoostOnTop()
        {
            _abilitySystem.ApplyEffect(_ship, _boost);

            _abilitySystem.SetBaseValue(_ship, _speed, 15f);

            _ship.TryGetAttribute(_speed, out var speed);
            Assert.That((speed.BaseValue, speed.CurrentValue), Is.EqualTo((15f, 30f)));
        }

        [Test]
        public void WhenTheBoostEnds_TheNewBaseRemains()
        {
            var boost = _abilitySystem.ApplyEffect(_ship, _boost);
            _abilitySystem.SetBaseValue(_ship, _speed, 15f);

            _abilitySystem.RemoveEffect(_ship, boost);

            _ship.TryGetAttribute(_speed, out var speed);
            Assert.That(speed.CurrentValue, Is.EqualTo(15f));
        }

        [Test]
        public void AnAttributeTheActorLacks_IsAddedAtTheBase()
        {
            var turn = ScriptableObject.CreateInstance<MoveSpeedAttribute>();
            turn.name = "Attr_Turn_Test";

            _abilitySystem.SetBaseValue(_ship, turn, 14f);

            Assert.That(_ship.TryGetAttribute(turn, out var value) ? value.CurrentValue : -1f, Is.EqualTo(14f));
            Object.DestroyImmediate(turn);
        }
    }
}
