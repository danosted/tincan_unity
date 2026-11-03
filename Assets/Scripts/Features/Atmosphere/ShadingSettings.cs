#nullable enable
using System;
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// A look's shading (.docs/ART_DIRECTION.md, "Light and shading"): how the TinCan shaders band their light. While a
    /// look applies, every material on TinCan/Stylized follows these instead of its own values, unless it keeps its own
    /// (its "Keep own" switches; islands keep theirs by default). The defaults are the shader's own, so moving the
    /// shading into the look changed nothing on screen. Plan: .docs/plans/artstyle-pipeline-breakdown.md.
    /// </summary>
    [Serializable]
    public struct ShadingSettings
    {
        [Tooltip("Width of the step between the bands, in N.L: 0 a hard line, 1 a plain gradient.")]
        [Range(0f, 1f)] public float BandSoftness;
        [Tooltip("2: lit and shadow. 3: a half-lit band between them.")]
        [Range(2, 3)] public int Bands;
        [Tooltip("Strength of the soft sky-coloured rim.")]
        [Range(0f, 1f)] public float Rim;
        [Tooltip("How much of the light's colour the shadow band keeps, tinted toward the shadow tint.")]
        [Range(0f, 1f)] public float ShadowFill;

        public static ShadingSettings Default => new()
        {
            BandSoftness = 0.35f,
            Bands = 2,
            Rim = 0.15f,
            ShadowFill = 0.45f,
        };

        /// <summary>As the shaders read it: x softness, y bands, z rim, w shadow fill.</summary>
        public Vector4 ToVector() => new(BandSoftness, Bands, Rim, ShadowFill);
    }
}
