#nullable enable
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// Large, faint patches a little warmer or cooler than the base colour, like paint laid on unevenly
    /// (Shaders/Include/Surface/ColourDrift.hlsl). It changes colour only; the bands stay clean.
    /// </summary>
    [CreateAssetMenu(fileName = "Surface_ColourDrift", menuName = "TinCan/Look/Surface Style: Colour Drift")]
    public sealed class ColourDriftSurfaceStyle : SurfaceStyle
    {
        public const string Keyword = "_TINCAN_SURFACE_DRIFT";

        [Tooltip("How far a patch leans warm or cool: 0 none (flat), 1 strong.")]
        [Range(0f, 1f)] public float Strength = 0.4f;
        [Tooltip("Size of a patch, in metres.")]
        [Min(0.1f)] public float PatchSize = 2.5f;

        public override SurfaceStyleFrame Evaluate() =>
            Strength <= 0f ? SurfaceStyleFrame.Flat : new SurfaceStyleFrame(Keyword, new Vector4(Strength, PatchSize, 0f, 0f));
    }
}
