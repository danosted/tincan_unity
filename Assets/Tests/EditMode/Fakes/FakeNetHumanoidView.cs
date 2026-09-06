#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Features.FreeCamera;
using TinCan.Features.HumanoidMovement;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>Humanoid fake with a controllable tag set, for systems that key off gameplay tags (net swing).</summary>
    public class FakeNetHumanoidView : IHumanoidCharacterView
    {
        private readonly HashSet<GameplayTag> _tags = new();

        public FakeNetHumanoidView(FakeHumanoidMovementView movement)
        {
            Movement = movement;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public HumanoidInputState InputState { get; set; }
        public ulong? PossessorId { get; private set; }
        public IHumanoidMovementView Movement { get; }
        public IOrbitalLookView Look { get; } = new FakeOrbitalLookView();
        public GameplayTagContainer ActiveTags => new(_tags);

        public void AuthoritativeSetPossessor(ulong? playerId) => PossessorId = playerId;
        public bool CanPossess(ulong playerId) => true;

        public bool HasTag(GameplayTag tag) => _tags.Contains(tag);
        public void AddTag(GameplayTag tag) => _tags.Add(tag);
        public void RemoveTag(GameplayTag tag) => _tags.Remove(tag);

        public bool TryGetAttribute(GameplayAttribute attribute, out AttributeValue value)
        {
            value = default;
            return false;
        }

        public void SetAttribute(GameplayAttribute attribute, AttributeValue value) { }
        public void ResetAttributesToBase() { }
        public void GrantAbility(IAbilityDefinition definition) { }
        public void RemoveAbility(IAbilityDefinition definition) { }
        public bool TryActivateAbility(IAbilityDefinition definition, IAbilityControllerBase? target = null) => false;
        public void HandleGameplayEvent(GameplayEventData eventData) { }
        public HumanoidAttributeSet GetAttributeSet() => null!;
        public bool TryGetAttributeSet<TAttributeSet>(out TAttributeSet set) where TAttributeSet : class, TinCan.Core.Domain.Abilities.Attributes.IAttributeSet
        {
            set = null!;
            return false;
        }
    }
}
