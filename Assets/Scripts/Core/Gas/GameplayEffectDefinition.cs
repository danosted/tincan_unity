#nullable enable
using UnityEngine;
using TinCan.Core.Domain.Abilities.Tags;
using System.Collections.Generic;

namespace TinCan.Core.Gas
{
    /// <summary>
    /// ScriptableObject defining a gameplay effect (e.g., Heal, Stun, Buff).
    /// </summary>
    [CreateAssetMenu(fileName = "New Effect", menuName = "TinCan/Abilities/Effect Definition")]
    public class GameplayEffectDefinition : ScriptableObject
    {
        public DurationType DurationType;
        public float DurationSeconds; // Only used if DurationType is Duration

        public List<AttributeModifier> Modifiers;
        public List<GameplayTag> GrantedTags;

        [Tooltip("Cue.* tags for presentation. Duration/Infinite: they join the target's tags while the effect is active " +
                 "(state cues, late joiners included). Instant: each fires once as a burst on every peer.")]
        public List<GameplayTag> Cues = new();
    }
}
