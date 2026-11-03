#nullable enable
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// How a surface is painted on top of its flat palette colour (plan .docs/plans/artstyle-pipeline-breakdown.md,
    /// "How visual approaches swap"). Each approach is one implementation, one shader keyword and one HLSL file under
    /// Shaders/Include/Surface/; the look profile names the active one and <see cref="LookShaderGlobals"/> applies it, so
    /// the presenter and the look tools never know which approach is in play.
    /// </summary>
    public interface ISurfaceStyle
    {
        SurfaceStyleFrame Evaluate();
    }

    /// <summary>What a surface style asks of the shaders: one keyword (none for flat) and up to four values, whose
    /// meaning is the style's own.</summary>
    public readonly struct SurfaceStyleFrame
    {
        public SurfaceStyleFrame(string? keyword, Vector4 values)
        {
            Keyword = keyword;
            Values = values;
        }

        public string? Keyword { get; }
        public Vector4 Values { get; }

        public static SurfaceStyleFrame Flat => new(null, Vector4.zero);
    }

    /// <summary>A surface style as an asset, so its values are tuned in the inspector and a look or a sweep can name it.</summary>
    public abstract class SurfaceStyle : ScriptableObject, ISurfaceStyle
    {
        public abstract SurfaceStyleFrame Evaluate();
    }

    /// <summary>Flat palette colour, nothing on top: the look as ART_DIRECTION's pillar 2 has it. What a look with no
    /// surface style uses.</summary>
    public sealed class FlatSurfaceStyle : ISurfaceStyle
    {
        public static readonly FlatSurfaceStyle Instance = new();

        private FlatSurfaceStyle()
        {
        }

        public SurfaceStyleFrame Evaluate() => SurfaceStyleFrame.Flat;
    }
}
