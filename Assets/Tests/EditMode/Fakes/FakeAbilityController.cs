using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Tests.EditMode.Fakes
{
    public class FakeAbilityController : IAbilityControllerBase
    {
        private readonly Dictionary<int, AttributeValue> _attributes = new();
        private readonly Dictionary<Type, IAttributeSet> _attributeSets = new();
        private readonly HashSet<GameplayTag> _tags = new();

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public GameplayTagContainer ActiveTags => new GameplayTagContainer(null);

        public bool HasTag(GameplayTag tag) => tag != null && _tags.Contains(tag);
        public void AddTag(GameplayTag tag) => _tags.Add(tag);
        public void RemoveTag(GameplayTag tag) => _tags.Remove(tag);

        public bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value)
        {
            if (attribute == null) { value = default; return false; }
            return _attributes.TryGetValue(attribute.GetHash(), out value);
        }

        public void SetAttribute(GameplayAttribute attribute, AttributeValue value)
            => _attributes[attribute.GetHash()] = value;

        public void ResetAttributesToBase()
        {
            foreach (var key in new List<int>(_attributes.Keys))
            {
                var value = _attributes[key];
                value.CurrentValue = value.BaseValue;
                _attributes[key] = value;
            }
        }

        public bool TryGetAttributeSet<TAttributeSet>(out TAttributeSet set) where TAttributeSet : class, IAttributeSet
        {
            set = _attributeSets.TryGetValue(typeof(TAttributeSet), out var found) ? found as TAttributeSet : null;
            return set != null;
        }

        public void RegisterAttributeSet(IAttributeSet set) => _attributeSets[set.GetType()] = set;

        public void GrantAbility(IAbilityDefinition definition) { }
        public void RemoveAbility(IAbilityDefinition definition) { }
        public bool TryActivateAbility(IAbilityDefinition definition, IAbilityControllerBase target = null) => false;
        public void HandleGameplayEvent(GameplayEventData eventData) { }
    }
}
