#nullable enable
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// The globals Shaders/Include/TinCanAtmosphere.hlsl reads: whether the banded air is on, and its colours and shape.
    /// Written by <see cref="AtmospherePresenter"/> in play and by the look's contact sheet in the editor.
    /// </summary>
    public static class AtmosphereShaderGlobals
    {
        private static readonly int AtmosphereOn = Shader.PropertyToID("_TinCanAtmosphereOn");
        private static readonly int SunwardFog = Shader.PropertyToID("_TinCanSunwardFog");
        private static readonly int AwayFog = Shader.PropertyToID("_TinCanAwayFog");
        private static readonly int FogParams = Shader.PropertyToID("_TinCanFogParams");
        private static readonly int FogShape = Shader.PropertyToID("_TinCanFogShape");

        /// <summary>Off, TinCan shaders take Unity's plain fog (opaque) or none (clouds).</summary>
        public static void SetEnabled(bool enabled) => Shader.SetGlobalFloat(AtmosphereOn, enabled ? 1f : 0f);

        public static void Apply(in AtmosphereFrame frame, in LookSettings look)
        {
            Shader.SetGlobalColor(SunwardFog, frame.SunwardFog);
            Shader.SetGlobalColor(AwayFog, frame.AwayFog);
            Shader.SetGlobalVector(FogParams, new Vector4(frame.FogDensity, frame.FogFalloff, look.FogBaseHeight, frame.HazeDensity));
            Shader.SetGlobalVector(FogShape, new Vector4(frame.MaxFog, look.SunwardFocus, 0f, 0f));
        }
    }
}
