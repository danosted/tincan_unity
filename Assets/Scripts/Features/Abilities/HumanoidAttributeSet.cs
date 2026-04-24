using System;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Attribute set wrapper for humanoid characters.
    /// Provides fast, type-safe C# properties backed by the generic NetworkList.
    /// </summary>
    public class HumanoidAttributeSet : IAttributeSet
    {
        private readonly IAbilityController _controller;

        public GameplayAttribute MoveSpeedDef { get; }
        public GameplayAttribute JumpForceDef { get; }
        public GameplayAttribute StaminaDef { get; }

        public HumanoidAttributeSet(IAbilityController controller, GameplayAttribute moveSpeed, GameplayAttribute jumpForce, GameplayAttribute stamina)
        {
            _controller = controller;
            MoveSpeedDef = moveSpeed;
            JumpForceDef = jumpForce;
            StaminaDef = stamina;
        }

        public float MoveSpeed
        {
            get
            {
                if (_controller.TryGetAttribute(MoveSpeedDef, out var val)) return val.CurrentValue;
                return 7f; // Default fallback
            }
        }

        public float JumpForce
        {
            get
            {
                if (_controller.TryGetAttribute(JumpForceDef, out var val)) return val.CurrentValue;
                return 8f; // Default fallback
            }
        }

        public float Stamina
        {
            get
            {
                if (_controller.TryGetAttribute(StaminaDef, out var val)) return val.CurrentValue;
                return 100f; // Default fallback
            }
        }

        public void InitializeBaseValues(float moveSpeed, float jumpForce, float stamina)
        {
            _controller.SetAttribute(MoveSpeedDef, new AttributeValue(moveSpeed));
            _controller.SetAttribute(JumpForceDef, new AttributeValue(jumpForce));
            _controller.SetAttribute(StaminaDef, new AttributeValue(stamina));
        }
    }
}
