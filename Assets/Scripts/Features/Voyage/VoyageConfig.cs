#nullable enable
using TinCan.Core.UI;
using UnityEngine;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Tunables for a voyage (Assets/Settings/Voyage/VoyageConfig, built by TinCan > Dev > Voyage > Build Assets).
    /// Plan: .docs/plans/voyage-session.md.
    /// </summary>
    [CreateAssetMenu(fileName = "VoyageConfig", menuName = "TinCan/Voyage/Voyage Config")]
    public class VoyageConfig : ScriptableObject
    {
        [Tooltip("The host starts the first voyage by itself once the ship exists. Off: only Restart starts one.")]
        public bool AutoStart = true;

        [Tooltip("Seconds of countdown before cast-off; hazards and breakage wait for it.")]
        [Min(0f)] public float BriefingSeconds = 10f;

        [Header("Route")]
        [Tooltip("Metres from the start to the destination, straight ahead of the ship's heading at the start. " +
                 "The ship flies about 15 m/s, so 4000 m is some 4.5 minutes at full throttle.")]
        [Min(10f)] public float RouteLength = 4000f;

        [Tooltip("Arrived once the ship is this close to the destination (measured level, ignoring height).")]
        [Min(1f)] public float ArrivalRadius = 60f;

        [Header("Presentation")]
        [Tooltip("End screen after a won voyage (Restart, Quit).")]
        public MenuDefinition? ArrivedMenu;

        [Tooltip("End screen after a lost voyage (Restart, Quit).")]
        public MenuDefinition? LostMenu;

        [Tooltip("The destination beacon's material (URP; an asset, so player builds include its shader).")]
        public Material? BeaconMaterial;

        [Min(1f)] public float BeaconHeight = 600f;
        [Min(0.1f)] public float BeaconWidth = 12f;
    }
}
