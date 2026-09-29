#nullable enable
namespace TinCan.Core.Gas
{
    [System.Serializable]
    public struct AttributeModifier
    {
        public TinCan.Core.Domain.Abilities.Attributes.GameplayAttribute Attribute;
        public ModifierOp Operation;
        public float Value;

        /// <summary>Optional upper bound for instant effects; the modified attribute is clamped to [0, this attribute's current value].</summary>
        public Core.Domain.Abilities.Attributes.GameplayAttribute? ClampMaxAttribute;
    }
}
