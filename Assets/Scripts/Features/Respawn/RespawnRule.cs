#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>What happens on a respawn for one <see cref="RespawnCause"/>. Edited in the <see cref="RespawnRules"/> asset.</summary>
    [Serializable]
    public sealed class RespawnRule
    {
        public RespawnCause Cause = RespawnCause.Overboard;

        [Tooltip("What happens to the item the player holds.")]
        public CarriedItemOutcome CarriedItem = CarriedItemOutcome.Lose;

        [Tooltip("Seconds between the fall and the respawn. 0 respawns as soon as the player chooses (handy while developing).")]
        [Min(0f)] public float DelaySeconds;
    }
}
