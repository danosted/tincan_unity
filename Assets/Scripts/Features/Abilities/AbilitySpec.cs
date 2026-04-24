using System;
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using UnityEngine;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Runtime state of a granted ability.
    /// </summary>
    public class AbilitySpec
    {
        public AbilityDefinition Definition { get; }
        public float LastActivatedTime { get; set; }
        public float StartTime { get; set; }
        public bool IsActive { get; set; }
        public HashSet<GameplayTag> ActiveWindowTags { get; } = new();
        public ActiveGameplayEffect AppliedActiveEffect { get; set; }

        public AbilitySpec(AbilityDefinition definition)
        {
            Definition = definition;
        }

        public bool IsOnCooldown(float currentTime)
        {
            if (Definition.CooldownEffect == null) return false;
            return currentTime < LastActivatedTime + Definition.CooldownEffect.Duration;
        }

        public void Activate(float currentTime)
        {
            IsActive = true;
            StartTime = currentTime;
            LastActivatedTime = currentTime;
            ActiveWindowTags.Clear();
        }
    }
}
