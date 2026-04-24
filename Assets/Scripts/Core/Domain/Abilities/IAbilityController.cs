using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using System.Collections.Generic;
using TinCan.Features.Abilities;

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

        bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value);
        void SetAttribute(GameplayAttribute attribute, AttributeValue value);
        void ResetAttributesToBase();

        void GrantAbility(AbilityDefinition definition);
        void RemoveAbility(AbilityDefinition definition);
        bool TryActivateAbility(AbilityDefinition definition);

        void HandleGameplayEvent(GameplayEventData eventData);
    }
    public interface IAbilityController<TAttributeSet> : IAbilityControllerBase where TAttributeSet : class, IAttributeSet
    {
        TAttributeSet GetAttributeSet();
    }
}
