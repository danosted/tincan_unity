#nullable enable

using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using UnityEngine;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Reusable health attribute set. Register it alongside an actor's control/stat set so every
    /// damageable actor shares one health surface and one calculation framework.
    /// </summary>
    public class HealthAttributeSet : IAttributeSet
    {
        private readonly IAbilityControllerBase _controller;
        private readonly float _brokenThreshold;

        public HealthAttribute HealthDef { get; }
        public MaxHealthAttribute MaxHealthDef { get; }

        /// <param name="brokenThreshold">Health fraction at or below which the actor counts as broken.</param>
        public HealthAttributeSet(
            IAbilityControllerBase controller,
            HealthAttribute healthDef,
            MaxHealthAttribute maxHealthDef,
            float brokenThreshold = 0f)
        {
            _controller = controller;
            HealthDef = healthDef;
            MaxHealthDef = maxHealthDef;
            _brokenThreshold = brokenThreshold;
        }

        public float Health => Read(HealthDef);
        public float MaxHealth => Read(MaxHealthDef);
        public float HealthPercentage => Percentage();
        public bool IsBroken => HealthPercentage <= Mathf.Clamp01(_brokenThreshold);

        public void InitializeBaseValues(float maxHealth)
        {
            _controller.SetAttribute(MaxHealthDef, new AttributeValue(maxHealth));
            _controller.SetAttribute(HealthDef, new AttributeValue(maxHealth));
        }

        private float Read(GameplayAttribute definition)
            => _controller.TryGetAttribute(definition, out var value) ? value.CurrentValue : 0f;

        private float Percentage()
        {
            if (MaxHealth <= 0f) return 0f;
            return Mathf.Clamp01(Health / MaxHealth);
        }
    }
}
