#nullable enable
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// The step between the lit and shadow bands breaks into ragged, stroke-like dabs
    /// (Shaders/Include/Surface/BrushEdge.hlsl). The step roughens but stays where it was; colour away from it is untouched.
    /// </summary>
    [CreateAssetMenu(fileName = "Surface_BrushEdge", menuName = "TinCan/Look/Surface Style: Brush Edge")]
    public sealed class BrushEdgeSurfaceStyle : SurfaceStyle
    {
        public const string Keyword = "_TINCAN_SURFACE_BRUSH";

        [Tooltip("How far the edge wanders, in N.L: 0 none (flat), 0.5 very ragged.")]
        [Range(0f, 0.5f)] public float Strength = 0.15f;
        [Tooltip("Size of a stroke, in metres.")]
        [Min(0.05f)] public float StrokeSize = 0.35f;

        public override SurfaceStyleFrame Evaluate() =>
            Strength <= 0f ? SurfaceStyleFrame.Flat : new SurfaceStyleFrame(Keyword, new Vector4(Strength, StrokeSize, 0f, 0f));
    }
}
