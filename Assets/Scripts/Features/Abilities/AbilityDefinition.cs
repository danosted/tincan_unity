using UnityEngine;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using System.Collections.Generic;

namespace TinCan.Features.Abilities
{
    [System.Serializable]
    public struct AbilityTagWindow
    {
        public GameplayTag Tag;
        public float StartOffset;
        public float Duration;
    }

    public enum AbilityInputPolicy { OnInputTriggered, OnInputHeld, OnInputReleased }
    public enum EffectTarget { Self, ProvidedTarget }

    /// <summary>
    /// ScriptableObject defining a gameplay ability's static properties.
    /// </summary>
    [CreateAssetMenu(fileName = "New Ability", menuName = "TinCan/Abilities/Ability Definition")]
    public class AbilityDefinition : ScriptableObject, IAbilityDefinition
    {
        [Header("Tags")]
        public GameplayTag AbilityTag; // Unique tag for this ability
        public List<GameplayTag> CancelAbilitiesWithTag;
        public List<GameplayTag> BlockAbilitiesWithTag;

        [Header("Activation Constraints")]
        public TinCan.Core.Domain.Abilities.Inputs.GameplayInput TriggerInput; // Input mapped to this ability
        public AbilityInputPolicy InputPolicy = AbilityInputPolicy.OnInputTriggered;
        public bool IsToggleable; // If true, re-triggering while active cancels it instead of being blocked
        public List<GameplayTag> ActivationRequiredTagsOnActor;
        public List<GameplayTag> ActivationBlockedTagsOnActor;
        public List<GameplayTag> ActivationRequiredTagsOnTarget;
        public List<GameplayTag> ActivationBlockedTagsOnTarget;
        public GameplayTag TriggerTag; // If this tag is received in a GameplayEvent, activate this ability

        [Header("Timing Windows (Tag-Based)")]
        public List<AbilityTagWindow> TimingTagWindows;

        [Header("Effects")]
        public GameplayEffectDefinition ActiveEffect; // The buff/debuff applied while active
        public EffectTarget ActiveEffectTarget = EffectTarget.Self; // Who gets the ActiveEffect
        public GameplayEffectDefinition CostEffect;
        public GameplayEffectDefinition CooldownEffect;

        // Logic for activation is usually handled by the UseCase or a subclass of this
    }
}
