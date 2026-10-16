#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// The one place that decides what happens when a player respawns: for each cause (falling overboard today), what
    /// happens to what they carry and how long they wait. The Respawn installer references one asset; a profile can use
    /// another (an instant one for development, one with a wait for live play). Plan: .docs/plans/overboard-and-rescue.md.
    /// </summary>
    [CreateAssetMenu(fileName = "RespawnRules", menuName = "TinCan/Respawn/Respawn Rules")]
    public sealed class RespawnRules : ScriptableObject
    {
        [Header("Rules")]
        [Tooltip("One rule per cause. A cause with no rule respawns at once and keeps what is carried.")]
        public List<RespawnRule> Rules = new() { new RespawnRule() };

        [Tooltip("Where on the ship a respawned player stands when the ship has no respawn point (ship space).")]
        public Vector3 BoardingOffset = new(0f, 3f, 0f);

        [Header("Leash (hang on, dragged behind the ship, until you climb or are winched up)")]
        [Tooltip("Rope length when you take the leash, metres from the ship's leash point: you are pulled up to it.")]
        [Min(1f)] public float RopeLength = 12f;

        [Tooltip("Whether a leashed player can climb their own rope (hold forward or jump). Off: only the winch brings them up.")]
        public bool CanClimb = true;

        [Tooltip("How fast climbing shortens the rope, m/s.")]
        [Min(0f)] public float ClimbSpeed = 2f;

        [Tooltip("How fast a cranked winch shortens the rope, m/s.")]
        [Min(0f)] public float WinchSpeed = 6f;

        [Tooltip("Within this distance of the leash point you are aboard: placed on deck at the point's stand spot.")]
        [Min(0.1f)] public float ArriveDistance = 1.5f;

        [Tooltip("Fastest the rope may drag you, m/s (a fast ship, or the yank when the leash catches you).")]
        [Min(1f)] public float MaxDragSpeed = 40f;

        [Tooltip("Material of the drawn rope (unlit). None: the default line material.")]
        public Material? RopeMaterial;

        [Tooltip("Width of the drawn rope, metres.")]
        [Min(0.001f)] public float RopeWidth = 0.05f;

        [Header("Identity")]
        [Tooltip("Applied to a player while they wait overboard (GE_Overboard); its tag marks them (State.Overboard).")]
        public GameplayEffectDefinition? OverboardEffect;

        [Tooltip("The tag the overboard effect grants: the player's own peer opens the choice while it is present.")]
        public GameplayTag? OverboardTag;

        [Tooltip("Applied to a player while they hang on the leash (GE_Leashed); its tag marks them (State.Leashed): only the server moves them then.")]
        public GameplayEffectDefinition? LeashedEffect;

        [Tooltip("The tag the leashed effect grants. It also switches on the Leashed input context (Interact: the respawn choice).")]
        public GameplayTag? LeashedTag;
    }
}
