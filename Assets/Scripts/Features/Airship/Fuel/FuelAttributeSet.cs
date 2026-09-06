#nullable enable
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Attribute wrapper for the fuel level (mirrors ModuleAttributeSet). The level lives in BaseValue and CurrentValue
    /// is always kept equal to it, because the ability system resets CurrentValue to BaseValue whenever any effect
    /// on the ship changes.
    /// </summary>
    public class FuelAttributeSet : IAttributeSet
    {
        private readonly IAbilityControllerBase _controller;

        public GameplayAttribute FuelDef { get; }

        public FuelAttributeSet(IAbilityControllerBase controller, GameplayAttribute fuelDef)
        {
            _controller = controller;
            FuelDef = fuelDef;
        }

        public bool HasValue => _controller.TryGetAttribute(FuelDef, out _);

        public float Level => _controller.TryGetAttribute(FuelDef, out var value) ? value.BaseValue : 0f;

        public void SetLevel(float level)
        {
            _controller.SetAttribute(FuelDef, new AttributeValue(level));
        }
    }
}
