#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// The storm lab's controls (Assets/Settings/Storm/StormLab): drag the sliders in the Inspector while Lab_Storm plays,
    /// or switch on Follow Zone and sail into the encounter zone. Changes made in Play stay in the asset, which is what a
    /// lab wants.
    /// </summary>
    [CreateAssetMenu(fileName = "StormLab", menuName = "TinCan/Storm/Storm Lab Settings")]
    public sealed class StormLabSettings : ScriptableObject
    {
        [Tooltip("0: calm sky outside the storm. 1: the eye's edge, by the heart.")]
        [Range(0f, 1f)] public float LabDepth = 0.5f;
        [Tooltip("1: raging. 0: cured (the storm comes apart).")]
        [Range(0f, 1f)] public float LabStrength = 1f;
        [Tooltip("Off: the sliders drive the storm. On: the encounter zone does, by where the camera is in it (sail in to see it), and the storm calms while the zone rests.")]
        public bool FollowZone;
    }
}
