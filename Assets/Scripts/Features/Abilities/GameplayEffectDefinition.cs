using UnityEngine;
using TinCan.Core.Domain.Abilities.Tags;
using System.Collections.Generic;

namespace TinCan.Features.Abilities
{
    public enum DurationType
    {
        Instant,
        Duration,
        Infinite
    }

    public enum ModifierOp
    {
        Add,
        Multiply,
        Override
    }

    [System.Serializable]
    public struct AttributeModifier
    {
        public TinCan.Core.Domain.Abilities.Attributes.GameplayAttribute Attribute;
        public ModifierOp Operation;
        public float Value;
    }

    /// <summary>
    /// ScriptableObject defining a gameplay effect (e.g., Heal, Stun, Buff).
    /// </summary>
    [CreateAssetMenu(fileName = "New Effect", menuName = "TinCan/Abilities/Effect Definition")]
    public class GameplayEffectDefinition : ScriptableObject
    {
        public DurationType DurationType;
        public float Duration; // Only used if DurationType is Duration

        public List<AttributeModifier> Modifiers;
        public List<GameplayTag> GrantedTags;
    }
}
