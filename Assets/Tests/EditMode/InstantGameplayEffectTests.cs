#nullable enable
using System.Collections.Generic;
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class InstantGameplayEffectTests
    {
        private HealthAttribute _healthDef = null!;
        private MaxHealthAttribute _maxHealthDef = null!;
        private GameplayEffectDefinition _effect = null!;
        private AbilitySystemUseCase _abilitySystem = null!;
        private FakeAbilityController _controller = null!;
        private HealthAttributeSet _health = null!;

        [SetUp]
        public void SetUp()
        {
            _healthDef = ScriptableObject.CreateInstance<HealthAttribute>();
            _healthDef.name = "Attr_Health_Test";
            _maxHealthDef = ScriptableObject.CreateInstance<MaxHealthAttribute>();
            _maxHealthDef.name = "Attr_MaxHealth_Test";

            _effect = ScriptableObject.CreateInstance<GameplayEffectDefinition>();
            _effect.DurationType = DurationType.Instant;
            _effect.GrantedTags = new List<GameplayTag>();

            _abilitySystem = new AbilitySystemUseCase(
                new FakeAbilityRegistry(),
                new FakeActorRegistry(),
                new FakeTimeService(),
                new FakeEventPublisher());

            _controller = new FakeAbilityController();
            _health = new HealthAttributeSet(_controller, _healthDef, _maxHealthDef);
            _health.InitializeBaseValues(1000f);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_healthDef);
            Object.DestroyImmediate(_maxHealthDef);
            Object.DestroyImmediate(_effect);
        }

        [Test]
        public void InstantDamage_PersistsAfterAttributeRecalculation()
        {
            _effect.Modifiers = new List<AttributeModifier>
            {
                new AttributeModifier
                {
                    Attribute = _healthDef,
                    Operation = ModifierOp.Add,
                    Value = -250f,
                    ClampMaxAttribute = _maxHealthDef
                }
            };

            _abilitySystem.ApplyEffect(_controller, _effect);

            Assert.That(_health.Health, Is.EqualTo(750f));
        }

        [Test]
        public void InstantDamage_ClampsAtZero()
        {
            _effect.Modifiers = new List<AttributeModifier>
            {
                new AttributeModifier
                {
                    Attribute = _healthDef,
                    Operation = ModifierOp.Add,
                    Value = -5000f,
                    ClampMaxAttribute = _maxHealthDef
                }
            };

            _abilitySystem.ApplyEffect(_controller, _effect);

            Assert.That(_health.Health, Is.EqualTo(0f));
            Assert.That(_health.IsBroken, Is.True);
        }

        [Test]
        public void InstantRepair_ClampsAtMaxHealth()
        {
            _controller.SetAttribute(_healthDef, new AttributeValue(900f));
            _effect.Modifiers = new List<AttributeModifier>
            {
                new AttributeModifier
                {
                    Attribute = _healthDef,
                    Operation = ModifierOp.Add,
                    Value = 500f,
                    ClampMaxAttribute = _maxHealthDef
                }
            };

            _abilitySystem.ApplyEffect(_controller, _effect);

            Assert.That(_health.Health, Is.EqualTo(1000f));
        }
    }
}
