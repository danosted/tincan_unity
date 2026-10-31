#nullable enable
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>Gas challenge tunables: the networked pocket a gas field spawns, how a field clears, and what a blast breaks.</summary>
    [CreateAssetMenu(fileName = "GasChallengeConfig", menuName = "TinCan/Gas Challenge Config")]
    public class GasChallengeConfig : ScriptableObject
    {
        [Tooltip("Networked pocket a gas field spawns (NetworkObject, EntityNetworkMediator, GasPocketVolume, GasPocketNetworkMediator).")]
        public GameObject? PocketPrefab;

        [Header("Gas field")]
        [Tooltip("A pocket this far (metres) behind the ship, along its heading, counts as passed: the field is cleared when every pocket has gone off or been passed.")]
        [Min(0f)] public float PassedDistance = 60f;

        [Tooltip("Depth of the band the pockets are spread over, metres along the heading, centred on the field's distance ahead.")]
        [Min(0f)] public float FieldDepth = 60f;

        [Tooltip("Height of the band, metres, centred on the ship's altitude.")]
        [Min(0f)] public float FieldHeight = 20f;

        [Tooltip("A spot is moved off rock (IWorldObstacleQuery) up to this many tries, then dropped.")]
        [Min(1)] public int PlacementTries = 6;

        [Header("Blast")]
        [Tooltip("Damage points within this many metres of the pocket's edge break when it goes off (fittings first).")]
        [Min(0f)] public float BlastRadius = 8f;

        [Tooltip("At most this many points break per pocket.")]
        [Min(0)] public int MaxBreaks = 2;
    }
}
