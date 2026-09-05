#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// Shared ability-controller fake. Stores attributes and attribute sets in plain dictionaries, records
    /// grants/activations, and simulates toggleable abilities that grant a tag while active
    /// (configure with <see cref="GrantsTagWhileActive"/>).
    /// </summary>
    public class FakeAbilityController : IAbilityControllerBase
    {
        private readonly Dictionary<int, AttributeValue> _attributes = new();
        private readonly Dictionary<Type, IAttributeSet> _attributeSets = new();
        private readonly Dictionary<IAbilityDefinition, GameplayTag> _tagByAbility = new();
        private GameplayTagContainer _tags = new(null);

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public GameplayTagContainer ActiveTags => _tags;
        public List<IAbilityDefinition> Granted { get; } = new();
        public List<IAbilityDefinition> Activations { get; } = new();
        public HashSet<IAbilityDefinition> Active { get; } = new();

        public void GrantsTagWhileActive(IAbilityDefinition ability, GameplayTag tag) => _tagByAbility[ability] = tag;

        public bool HasTag(GameplayTag tag) => tag != null && _tags.HasTag(tag);
        public void AddTag(GameplayTag tag) => _tags.AddTag(tag);
        public void RemoveTag(GameplayTag tag) => _tags.RemoveTag(tag);

        public bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value)
        {
            if (attribute == null) { value = default; return false; }
            return _attributes.TryGetValue(attribute.GetHash(), out value);
        }

        public void SetAttribute(GameplayAttribute attribute, AttributeValue value) => _attributes[attribute.GetHash()] = value;

        public void ResetAttributesToBase()
        {
            foreach (var key in new List<int>(_attributes.Keys))
            {
                var v = _attributes[key];
                v.CurrentValue = v.BaseValue;
                _attributes[key] = v;
            }
        }

        public bool TryGetAttributeSet<TAttributeSet>(out TAttributeSet set) where TAttributeSet : class, IAttributeSet
        {
            set = (_attributeSets.TryGetValue(typeof(TAttributeSet), out var found) ? found as TAttributeSet : null)!;
            return set != null;
        }

        public void RegisterAttributeSet(IAttributeSet set) => _attributeSets[set.GetType()] = set;

        public void GrantAbility(IAbilityDefinition definition)
        {
            if (!Granted.Contains(definition)) Granted.Add(definition);
        }

        public void RemoveAbility(IAbilityDefinition definition)
        {
            Granted.Remove(definition);
            Active.Remove(definition);
        }

        public bool TryActivateAbility(IAbilityDefinition definition, IAbilityControllerBase? target = null)
        {
            if (!Granted.Contains(definition)) return false;
            Activations.Add(definition);

            _tagByAbility.TryGetValue(definition, out var tag);
            if (Active.Remove(definition))
            {
                if (tag != null) RemoveTag(tag);
                return true;
            }

            Active.Add(definition);
            if (tag != null) AddTag(tag);
            return true;
        }

        public void HandleGameplayEvent(GameplayEventData eventData) { }
    }
}
