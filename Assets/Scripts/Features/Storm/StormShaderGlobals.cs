#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// The storm's region for the shaders (Shaders/Include/TinCanStorm.hlsl): where it stands and how strong it is, so the
    /// clouds over it tower and darken for everyone, wherever they look from; and its lightning, lighting the cloud round
    /// the strike.
    /// </summary>
    public static class StormShaderGlobals
    {
        private static readonly int Region = Shader.PropertyToID("_TinCanStormRegion");
        private static readonly int FlashSpot = Shader.PropertyToID("_TinCanStormFlash");
        private static readonly int FlashColour = Shader.PropertyToID("_TinCanStormFlashColour");

        public static void Apply(Vector2 centre, float radius, float strength) =>
            Shader.SetGlobalVector(Region, new Vector4(centre.x, centre.y, Mathf.Max(0f, radius), Mathf.Clamp01(strength)));

        /// <summary>A strike's glow at <paramref name="spot"/>, in <paramref name="colour"/> times <paramref name="flash"/>
        /// (0: none now).</summary>
        public static void ApplyFlash(Vector3 spot, Color colour, float flash)
        {
            Shader.SetGlobalVector(FlashSpot, flash > 0f ? new Vector4(spot.x, spot.y, spot.z, StormProcessor.GlowRadius) : Vector4.zero);
            Shader.SetGlobalVector(FlashColour, (Vector4)(colour * flash));
        }

        /// <summary>No storm in the world (radius 0) and no flash.</summary>
        public static void Clear()
        {
            Shader.SetGlobalVector(Region, Vector4.zero);
            Shader.SetGlobalVector(FlashSpot, Vector4.zero);
        }
    }
}
