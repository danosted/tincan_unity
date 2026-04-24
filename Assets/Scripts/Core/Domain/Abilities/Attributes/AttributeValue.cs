using System;

namespace TinCan.Core.Domain.Abilities.Attributes
{
    /// <summary>
    /// Represents a single attribute value with a base value and a calculated current value.
    /// </summary>
    [Serializable]
    public struct AttributeValue
    {
        public float BaseValue;
        public float CurrentValue;

        public AttributeValue(float baseValue)
        {
            BaseValue = baseValue;
            CurrentValue = baseValue;
        }
    }
}
