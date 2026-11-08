#nullable enable
using System.Collections.Generic;
using TinCan.Features.Gates;
using UnityEngine;

namespace TinCan.Features.Boons
{
    /// <summary>Tunables for boon gates (Assets/Settings/Boons/BoonsConfig). Plan: .docs/plans/hazards-and-boons.md (B1).</summary>
    [CreateAssetMenu(fileName = "BoonsConfig", menuName = "TinCan/Boons/Boons Config")]
    public class BoonsConfig : ScriptableObject
    {
        [Tooltip("The boon gates a voyage can lay (Gate_Boon_SpeedBurst): each one's action gives a boon. Each gate along " +
                 "the route picks one from the voyage seed.")]
        public List<GateDefinition> Gates = new();

        [Header("Placement (from the voyage seed, the same on every peer)")]
        [Tooltip("Metres along the route between gates.")]
        [Min(50f)] public float Spacing = 700f;
        [Tooltip("How far to either side of the straight route a gate sits (metres): off the route, so going for it is " +
                 "a steering choice.")]
        public Vector2 LateralOffset = new(120f, 220f);
        [Tooltip("A gate sits up to this many metres above or below the route.")]
        [Min(0f)] public float AltitudeJitter = 30f;
        [Tooltip("No gate within this many metres of the voyage's start or destination.")]
        [Min(0f)] public float KeepClear = 250f;

        public BoonGateRules Rules => new(Spacing, LateralOffset, AltitudeJitter, KeepClear, Gates.Count);

        /// <summary>The gate a placement picked, or null if the list changed under it.</summary>
        public GateDefinition? GateAt(int index) => index >= 0 && index < Gates.Count ? Gates[index] : null;
    }
}
