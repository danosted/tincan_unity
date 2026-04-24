using System;
using UnityEngine;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Runtime state of an active gameplay effect.
    /// </summary>
    public class ActiveGameplayEffect
    {
        public GameplayEffectDefinition Definition { get; }
        public float StartTime { get; }
        public float ExpiryTime { get; }

        public ActiveGameplayEffect(GameplayEffectDefinition definition, float currentTime)
        {
            Definition = definition;
            StartTime = currentTime;
            ExpiryTime = definition.DurationType == DurationType.Duration
                ? currentTime + definition.Duration
                : float.MaxValue;
        }

        public bool IsExpired(float currentTime)
        {
            if (Definition.DurationType == DurationType.Instant) return true;
            if (Definition.DurationType == DurationType.Infinite) return false;
            return currentTime >= ExpiryTime;
        }
    }
}
