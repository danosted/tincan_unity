using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using System.Collections.Generic;

namespace TinCan.Core.Domain.Abilities
{
    /// <summary>
    /// Domain interface for managing an actor's abilities, tags, and attributes.
    /// </summary>
    public interface IAbilityControllerBase : IActor
    {
        GameplayTagContainer ActiveTags { get; }

        bool HasTag(GameplayTag tag);
        void AddTag(GameplayTag tag);
        void RemoveTag(GameplayTag tag);

        // Effect tags follow the simulation: authoritative on the server, predicted locally by the owner.
        // Controllers without networking can use the same storage as ordinary tags.
        void AddEffectTag(GameplayTag tag) => AddTag(tag);
        void RemoveEffectTag(GameplayTag tag) => RemoveTag(tag);

        bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value);
        void SetAttribute(GameplayAttribute attribute, AttributeValue value);
        void ResetAttributesToBase();

        /// <summary>
        /// Sets an attribute's base value and keeps its active effects on top (the networked controller goes through
        /// the ability system). This default, for a controller without effects, makes the current value the base.
        /// </summary>
        void SetBaseValue(GameplayAttribute attribute, float value) => SetAttribute(attribute, new AttributeValue(value));

        bool TryGetAttributeSet<TAttributeSet>(out TAttributeSet set) where TAttributeSet : class, IAttributeSet;

        /// <summary>Makes a set findable through <see cref="TryGetAttributeSet{TAttributeSet}"/>; one set per type.</summary>
        void RegisterAttributeSet(IAttributeSet set);

        void GrantAbility(IAbilityDefinition definition);
        void RemoveAbility(IAbilityDefinition definition);
        bool TryActivateAbility(IAbilityDefinition definition, IAbilityControllerBase target = null);

        void HandleGameplayEvent(GameplayEventData eventData);
    }
    public interface IAbilityController<TAttributeSet> : IAbilityControllerBase where TAttributeSet : class, IAttributeSet
    {
        TAttributeSet GetAttributeSet();
    }
}
