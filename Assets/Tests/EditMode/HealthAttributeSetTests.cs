#nullable enable
using NUnit.Framework;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Abilities;
using TinCan.Tests.EditMode.Fakes;
using UnityEngine;

namespace TinCan.Tests.EditMode
{
    public class HealthAttributeSetTests
    {
        private HealthAttribute _healthDef = null!;
        private MaxHealthAttribute _maxHealthDef = null!;
        private FakeAbilityController _controller = null!;

        [SetUp]
        public void SetUp()
        {
            _healthDef = ScriptableObject.CreateInstance<HealthAttribute>();
            _healthDef.name = "Attr_Health_Test";
            _maxHealthDef = ScriptableObject.CreateInstance<MaxHealthAttribute>();
            _maxHealthDef.name = "Attr_MaxHealth_Test";
            _controller = new FakeAbilityController();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_healthDef);
            Object.DestroyImmediate(_maxHealthDef);
        }

        [Test]
        public void InitializeBaseValues_SpawnsAtFullHealth()
        {
            var set = new HealthAttributeSet(_controller, _healthDef, _maxHealthDef);
            set.InitializeBaseValues(1000f);

            Assert.That(set.Health, Is.EqualTo(1000f));
            Assert.That(set.MaxHealth, Is.EqualTo(1000f));
            Assert.That(set.HealthPercentage, Is.EqualTo(1f));
            Assert.That(set.IsBroken, Is.False);
        }

        [Test]
        public void HealthPercentage_ReflectsCurrentValue()
        {
            var set = new HealthAttributeSet(_controller, _healthDef, _maxHealthDef);
            set.InitializeBaseValues(1000f);
            _controller.SetAttribute(_healthDef, new AttributeValue(750f));

            Assert.That(set.HealthPercentage, Is.EqualTo(0.75f));
            Assert.That(set.IsBroken, Is.False);
        }

        [Test]
        public void IsBroken_WhenHealthIsDepleted()
        {
            var set = new HealthAttributeSet(_controller, _healthDef, _maxHealthDef);
            set.InitializeBaseValues(1000f);
            _controller.SetAttribute(_healthDef, new AttributeValue(0f));

            Assert.That(set.IsBroken, Is.True);
        }

        [Test]
        public void IsBroken_UsesConfiguredThreshold()
        {
            var set = new HealthAttributeSet(_controller, _healthDef, _maxHealthDef, 0.1f);
            set.InitializeBaseValues(100f);
            _controller.SetAttribute(_healthDef, new AttributeValue(10f));

            Assert.That(set.IsBroken, Is.True);

            _controller.SetAttribute(_healthDef, new AttributeValue(11f));

            Assert.That(set.IsBroken, Is.False);
        }
    }
}
