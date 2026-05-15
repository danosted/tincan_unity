using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Attribute set for individual ship modules.
    /// Tracks health and potentially other module-specific stats.
    /// </summary>
    public class ModuleAttributeSet : IAttributeSet
    {
        private readonly IAbilityControllerBase _controller;

        public GameplayAttribute HealthDef { get; }

        public ModuleAttributeSet(IAbilityControllerBase controller, GameplayAttribute healthDef)
        {
            _controller = controller;
            HealthDef = healthDef;
        }

        public float Health
        {
            get
            {
                if (_controller.TryGetAttribute(HealthDef, out var val)) return val.CurrentValue;
                return 100f;
            }
        }

        public void InitializeBaseValues(float health)
        {
            _controller.SetAttribute(HealthDef, new AttributeValue(health));
        }
    }
}
