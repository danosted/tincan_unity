#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.CloudMist
{
    /// <summary>
    /// How well the mist is sampled on one quality tier, which is what it costs (plan .docs/plans/cloud-sea-fog.md, F4).
    /// Never where the mist is or how far it reaches: its look is the mist material's, and its reach a share of the view
    /// distance (<see cref="CloudMistQuality.Reach"/>), the same on every tier.
    /// </summary>
    [Serializable]
    public struct CloudMistTier
    {
        [Tooltip("The tier's name, as the graphics.clouds setting names it (Low, Medium, High, Ultra).")]
        public string Name;
        [Tooltip("The march's resolution, a share of the screen's width and height (0.5: a quarter of the pixels).")]
        [Range(0.125f, 1f)] public float Resolution;
        [Tooltip("Metres of mist per step along a view ray, on average (finer near the eye). A ray takes as many steps as " +
                 "its path through the mist needs, so a longer view distance costs more steps, up to Max Steps.")]
        [Range(1f, 50f)] public float StepLength;
        [Tooltip("The most steps one view ray takes. Past it the steps stretch, so the far mist coarsens instead of costing more.")]
        [Range(4, 64)] public int MaxSteps;
        [Tooltip("Steps toward the sun from each step, for self-shadowing. 0: lit by depth into the cloud only.")]
        [Range(0, 6)] public int LightSteps;
        [Tooltip("Octaves of the drifting detail noise. 0: the mist's shape only.")]
        [Range(0, 3)] public int DetailOctaves;
        [Tooltip("The sun's shadows (ship, islands) on the mist's top, one sample per pixel (Shadow Darken on the material).")]
        public bool Shadows;
        [Tooltip("How far the sun's shadows reach while this tier draws, in metres, so the ship's and the islands' shadows " +
                 "land on the mist: the game's shadow distance grows to it, in cascades that keep the game's own near " +
                 "shadows as sharp (CloudMistShadowRange). Costs the shadow casters drawn again per cascade. 0: the game's own.")]
        [Range(0f, 1500f)] public float ShadowRange;

        public CloudMistTier(string name, float resolution, float stepLength, int maxSteps, int lightSteps, int detailOctaves,
            bool shadows, float shadowRange = 0f)
        {
            Name = name;
            Resolution = resolution;
            StepLength = stepLength;
            MaxSteps = maxSteps;
            LightSteps = lightSteps;
            DetailOctaves = detailOctaves;
            Shadows = shadows;
            ShadowRange = shadowRange;
        }
    }
}
