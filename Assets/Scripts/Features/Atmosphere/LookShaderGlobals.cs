#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// The globals and keywords Shaders/Include/TinCanLighting.hlsl reads for a look's shading and surface style. Written
    /// by <see cref="AtmospherePresenter"/> in play and by the look tools in the editor (which switch it off again after
    /// a render), so edit mode and play show the same look. Off, every shader uses its material's own values and no style.
    /// Plan: .docs/plans/artstyle-pipeline-breakdown.md.
    /// </summary>
    public static class LookShaderGlobals
    {
        private static readonly int ShadingOn = Shader.PropertyToID("_TinCanShadingOn");
        private static readonly int Shading = Shader.PropertyToID("_TinCanShading");
        private static readonly int SurfaceParams = Shader.PropertyToID("_TinCanSurfaceParams");

        /// <summary>Every surface style's keyword, as the TinCan shaders declare them. A new style adds its own here.</summary>
        public static readonly IReadOnlyList<string> SurfaceKeywords = new[]
        {
            ColourDriftSurfaceStyle.Keyword,
            BrushEdgeSurfaceStyle.Keyword,
        };

        public static void Apply(in ShadingSettings shading, in SurfaceStyleFrame surface)
        {
            Shader.SetGlobalVector(Shading, shading.ToVector());
            Shader.SetGlobalVector(SurfaceParams, surface.Values);
            foreach (var keyword in SurfaceKeywords)
            {
                if (keyword == surface.Keyword) Shader.EnableKeyword(keyword);
                else Shader.DisableKeyword(keyword);
            }
        }

        /// <summary>Off, materials keep their own shading and the surface style keywords are cleared.</summary>
        public static void SetEnabled(bool enabled)
        {
            Shader.SetGlobalFloat(ShadingOn, enabled ? 1f : 0f);
            if (enabled) return;
            foreach (var keyword in SurfaceKeywords) Shader.DisableKeyword(keyword);
        }

        public static bool IsEnabled => Shader.GetGlobalFloat(ShadingOn) > 0.5f;

        public static Vector4 CurrentShading => Shader.GetGlobalVector(Shading);
    }
}
