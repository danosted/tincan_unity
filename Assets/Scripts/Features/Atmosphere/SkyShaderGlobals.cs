#nullable enable
using UnityEngine;

namespace TinCan.Features.Atmosphere
{
    /// <summary>
    /// The globals the painted sky (Shaders/PaintedSky.shader) and the night shade (Shaders/Include/TinCanAtmosphere.hlsl)
    /// read: the day's zenith, the belt of Venus and how far fixed-colour surfaces darken at night. Off, the sky takes its
    /// material's colours and nothing darkens. Written by the cloud lab's day (DevTools/Editor/CloudLabDay) in the editor.
    /// Plan: .docs/plans/day-cycle.md.
    /// </summary>
    public static class SkyShaderGlobals
    {
        private static readonly int SkyOn = Shader.PropertyToID("_TinCanSkyOn");
        private static readonly int SkyZenith = Shader.PropertyToID("_TinCanSkyZenith");
        private static readonly int SkyBelt = Shader.PropertyToID("_TinCanSkyBelt");
        private static readonly int NightShade = Shader.PropertyToID("_TinCanNightShade");

        public static void SetEnabled(bool enabled)
        {
            Shader.SetGlobalFloat(SkyOn, enabled ? 1f : 0f);
            if (!enabled) Shader.SetGlobalColor(NightShade, Color.clear);
        }

        public static void Apply(in DayLightKey light)
        {
            Shader.SetGlobalColor(SkyZenith, light.SkyZenith);
            Shader.SetGlobalColor(SkyBelt, light.SkyBelt);
            Shader.SetGlobalColor(NightShade, light.NightShade);
        }
    }
}
