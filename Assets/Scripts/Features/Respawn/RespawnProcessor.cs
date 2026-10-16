#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>Pure respawn rules: which rule applies to a cause, and how long until a respawn may happen.</summary>
    public sealed class RespawnProcessor
    {
        private static readonly RespawnRule Default = new() { CarriedItem = CarriedItemOutcome.Keep, DelaySeconds = 0f };

        /// <summary>The rule for <paramref name="cause"/>; with none listed, respawn at once and keep what is carried.</summary>
        public static RespawnRule RuleFor(IReadOnlyList<RespawnRule> rules, RespawnCause cause)
        {
            foreach (var rule in rules)
            {
                if (rule != null && rule.Cause == cause) return rule;
            }

            return Default;
        }

        /// <summary>Seconds left before a player who fell at <paramref name="fellAt"/> may respawn (0 when they may).</summary>
        public static float Remaining(float fellAt, float now, float delaySeconds) => Mathf.Max(0f, fellAt + delaySeconds - now);
    }
}
