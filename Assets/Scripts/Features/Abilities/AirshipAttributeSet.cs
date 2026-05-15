using System;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Attribute set wrapper for the Airship.
    /// Bridges the Gameplay Ability System with the Airship's movement properties.
    /// </summary>
    public class AirshipAttributeSet : IAttributeSet
    {
        private readonly IAbilityControllerBase _controller;

        public GameplayAttribute FlightSpeedDef { get; }
        public GameplayAttribute TurnSpeedDef { get; }
        public GameplayAttribute HealthDef { get; }

        public AirshipAttributeSet(
            IAbilityControllerBase controller,
            GameplayAttribute flightSpeed,
            GameplayAttribute turnSpeed,
            GameplayAttribute health)
        {
            _controller = controller;
            FlightSpeedDef = flightSpeed;
            TurnSpeedDef = turnSpeed;
            HealthDef = health;
        }

        public float MoveSpeed
        {
            get
            {
                if (_controller.TryGetAttribute(FlightSpeedDef, out var val)) return val.CurrentValue;
                return 20f; // Default fallback
            }
        }

        public float TurnSpeed
        {
            get
            {
                if (_controller.TryGetAttribute(TurnSpeedDef, out var val)) return val.CurrentValue;
                return 45f; // Default fallback
            }
        }

        public float Health
        {
            get
            {
                if (_controller.TryGetAttribute(HealthDef, out var val)) return val.CurrentValue;
                return 100f; // Default fallback
            }
        }

        public void InitializeBaseValues(float moveSpeed, float turnSpeed, float health)
        {
            _controller.SetAttribute(FlightSpeedDef, new AttributeValue(moveSpeed));
            _controller.SetAttribute(TurnSpeedDef, new AttributeValue(turnSpeed));
            _controller.SetAttribute(HealthDef, new AttributeValue(health));
        }
    }
}
