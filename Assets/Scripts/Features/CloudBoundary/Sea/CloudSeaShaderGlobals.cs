#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The globals the sea shader reads (Shaders/Include/TinCanCloudSea.hlsl): the shared time, the base altitude, the
    /// swell, each hole's shape now, and the web of rifts. Set each frame by <see cref="CloudSeaPresenter"/>.
    /// </summary>
    public static class CloudSeaShaderGlobals
    {
        private static readonly int Sea = Shader.PropertyToID("_TinCanCloudSea");
        private static readonly int Swell = Shader.PropertyToID("_TinCanCloudSwell");
        private static readonly int Holes = Shader.PropertyToID("_TinCanCloudHoles");
        private static readonly int HoleOpenness = Shader.PropertyToID("_TinCanCloudHoleOpenness");
        private static readonly int HoleReach = Shader.PropertyToID("_TinCanCloudHoleReach");
        private static readonly int HoleCount = Shader.PropertyToID("_TinCanCloudHoleCount");
        private static readonly int Web = Shader.PropertyToID("_TinCanCloudWeb");
        private static readonly int WebMotion = Shader.PropertyToID("_TinCanCloudWebMotion");
        private static readonly Vector4[] HoleBuffer = new Vector4[ICloudHoles.MaxHoles];
        private static readonly float[] OpennessBuffer = new float[ICloudHoles.MaxHoles];
        private static readonly Vector4[] ReachBuffer = new Vector4[ICloudHoles.MaxHoles];

        /// <param name="fadeOut">Metres from the camera where the drawn surface has faded out.</param>
        public static void Apply(float time, float baseAltitude, in CloudSwell swell, IReadOnlyList<CloudHoleShape> holes,
            in CloudWeb web, float fadeOut)
        {
            Shader.SetGlobalVector(Sea, new Vector4(time, baseAltitude, swell.Amplitude, swell.Wavelength));
            Shader.SetGlobalVector(Swell, new Vector4(swell.Speed, swell.Offset.x, swell.Offset.y, fadeOut));
            Shader.SetGlobalVector(Web, new Vector4(web.Cell, web.Width, web.Rim, web.Ragged));
            Shader.SetGlobalVector(WebMotion, new Vector4(web.OpenShare, web.Drift, web.Offset.x, web.Offset.y));

            int count = Mathf.Min(holes.Count, ICloudHoles.MaxHoles);
            for (int i = 0; i < ICloudHoles.MaxHoles; i++)
            {
                CloudHoleShape hole = i < count ? holes[i] : default;
                HoleBuffer[i] = new Vector4(hole.Centre.x, hole.Centre.y, hole.Radius, hole.Rim);
                OpennessBuffer[i] = hole.Openness;
                ReachBuffer[i] = new Vector4(hole.Reach.x, hole.Reach.y, hole.Ragged, 0f);
            }

            Shader.SetGlobalVectorArray(Holes, HoleBuffer);
            Shader.SetGlobalFloatArray(HoleOpenness, OpennessBuffer);
            Shader.SetGlobalVectorArray(HoleReach, ReachBuffer);
            Shader.SetGlobalFloat(HoleCount, count);
        }

        private static readonly int LookOn = Shader.PropertyToID("_TinCanCloudLookOn");
        private static readonly int LookLit = Shader.PropertyToID("_TinCanCloudLookLit");
        private static readonly int LookShadow = Shader.PropertyToID("_TinCanCloudLookShadow");
        private static readonly int LookHollow = Shader.PropertyToID("_TinCanCloudLookHollow");
        private static readonly int LookLight = Shader.PropertyToID("_TinCanCloudLookLight");
        private static readonly int LookSilver = Shader.PropertyToID("_TinCanCloudLookSilver");

        /// <summary>
        /// The sea material's look (colours and light knobs) as globals, for the volumetric sky's painted mode
        /// (Shaders/Include/TinCanCloudLook.hlsl, <c>TinCanCloudLookFromGlobals</c>), so the sky takes the sea's colours.
        /// </summary>
        public static void ApplyLook(Material sea)
        {
            static Vector4 Colour(Material m, string name, float a)
            {
                Color c = m.GetColor(name);
                return new Vector4(c.r, c.g, c.b, a);
            }

            Shader.SetGlobalVector(LookLit, Colour(sea, "_LitColour", sea.GetFloat("_HollowDarken")));
            Shader.SetGlobalVector(LookShadow, Colour(sea, "_ShadowColour", sea.GetFloat("_SunTint")));
            Shader.SetGlobalVector(LookHollow, Colour(sea, "_HollowColour", sea.GetFloat("_Ambient")));
            Shader.SetGlobalVector(LookLight, new Vector4(sea.GetFloat("_Wrap"), sea.GetFloat("_BandSoftness"), sea.GetFloat("_Bands"), sea.GetFloat("_Rim")));
            Shader.SetGlobalVector(LookSilver, new Vector4(sea.GetFloat("_RimPower"), sea.GetFloat("_SilverLining"), sea.GetFloat("_SilverFocus"), sea.GetFloat("_Translucency")));
            Shader.SetGlobalFloat(LookOn, 1f);
        }

        public static void Clear()
        {
            Shader.SetGlobalFloat(HoleCount, 0f);
            Shader.SetGlobalVector(WebMotion, Vector4.zero);
            Shader.SetGlobalFloat(LookOn, 0f);
        }
    }
}
