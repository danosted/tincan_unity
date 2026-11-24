#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// How well the mist is sampled on one quality tier (plan .docs/plans/cloud-sea-fog.md, F4). Never where the mist is
    /// or what it looks like: that is the mist material's, the same on every tier.
    /// </summary>
    [Serializable]
    public struct CloudMistTier
    {
        [Tooltip("The tier's name, as the graphics.clouds setting names it (Low, Medium, High, Ultra).")]
        public string Name;
        [Tooltip("The march's resolution, a share of the screen's width and height (0.5: a quarter of the pixels).")]
        [Range(0.125f, 1f)] public float Resolution;
        [Tooltip("Steps along each view ray through the mist, finer near the eye.")]
        [Range(4, 64)] public int Steps;
        [Tooltip("Steps toward the sun from each step, for self-shadowing. 0: lit by depth into the cloud only.")]
        [Range(0, 6)] public int LightSteps;
        [Tooltip("Octaves of the drifting detail noise. 0: the mist's shape only.")]
        [Range(0, 3)] public int DetailOctaves;
        [Tooltip("The sun's shadows (ship, islands) in the mist.")]
        public bool Shadows;
        [Tooltip("How far from the camera the mist reaches, in metres; it fades out over its last part.")]
        [Range(50f, 2000f)] public float Range;

        public CloudMistTier(string name, float resolution, int steps, int lightSteps, int detailOctaves, bool shadows, float range)
        {
            Name = name;
            Resolution = resolution;
            Steps = steps;
            LightSteps = lightSteps;
            DetailOctaves = detailOctaves;
            Shadows = shadows;
            Range = range;
        }
    }
}
