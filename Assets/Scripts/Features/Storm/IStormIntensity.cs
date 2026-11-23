#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// How much storm the local view is in: <see cref="Depth"/> (0 outside the wall, 1 at the eye's edge; per peer, from
    /// where the camera is) and <see cref="Strength"/> (1 raging, 0 cured; replicated on the zone in the world). In the
    /// lab both come from <see cref="StormLabSettings"/>; the zone provides them from ST2.
    /// </summary>
    public interface IStormIntensity
    {
        float Depth { get; }
        float Strength { get; }
    }

    /// <summary>
    /// The storm lab's two sliders (Assets/Settings/Storm/StormLab): drag them in the Inspector while Lab_Storm plays.
    /// Changes made in Play stay in the asset, which is what a lab wants.
    /// </summary>
    [CreateAssetMenu(fileName = "StormLab", menuName = "TinCan/Storm/Storm Lab Settings")]
    public sealed class StormLabSettings : ScriptableObject, IStormIntensity
    {
        [Tooltip("0: calm sky outside the storm. 1: the eye's edge, by the heart.")]
        [Range(0f, 1f)] public float LabDepth = 0.5f;
        [Tooltip("1: raging. 0: cured (the storm comes apart).")]
        [Range(0f, 1f)] public float LabStrength = 1f;

        public float Depth => LabDepth;
        public float Strength => LabStrength;
    }
}
