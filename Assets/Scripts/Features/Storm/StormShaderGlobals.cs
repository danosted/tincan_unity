#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// The storm's region for the shaders (Shaders/Include/TinCanStorm.hlsl): where it stands and how strong it is, so the
    /// clouds over it tower and darken for everyone, wherever they look from.
    /// </summary>
    public static class StormShaderGlobals
    {
        private static readonly int Region = Shader.PropertyToID("_TinCanStormRegion");

        public static void Apply(Vector2 centre, float radius, float strength) =>
            Shader.SetGlobalVector(Region, new Vector4(centre.x, centre.y, Mathf.Max(0f, radius), Mathf.Clamp01(strength)));

        /// <summary>No storm in the world (radius 0).</summary>
        public static void Clear() => Shader.SetGlobalVector(Region, Vector4.zero);
    }
}
