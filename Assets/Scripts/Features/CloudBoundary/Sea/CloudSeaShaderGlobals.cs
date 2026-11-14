#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The globals the sea shader reads (Shaders/Include/TinCanCloudSea.hlsl): the shared time, the base altitude, the
    /// swell, and each hole's shape now. Set each frame by <see cref="CloudSeaPresenter"/>.
    /// </summary>
    public static class CloudSeaShaderGlobals
    {
        private static readonly int Sea = Shader.PropertyToID("_TinCanCloudSea");
        private static readonly int Swell = Shader.PropertyToID("_TinCanCloudSwell");
        private static readonly int Holes = Shader.PropertyToID("_TinCanCloudHoles");
        private static readonly int HoleOpenness = Shader.PropertyToID("_TinCanCloudHoleOpenness");
        private static readonly int HoleCount = Shader.PropertyToID("_TinCanCloudHoleCount");
        private static readonly Vector4[] HoleBuffer = new Vector4[ICloudHoles.MaxHoles];
        private static readonly float[] OpennessBuffer = new float[ICloudHoles.MaxHoles];

        /// <param name="gridHalf">Half the drawn grid's width, where its edge has faded out.</param>
        public static void Apply(float time, float baseAltitude, in CloudSwell swell, IReadOnlyList<CloudHoleShape> holes, float gridHalf)
        {
            Shader.SetGlobalVector(Sea, new Vector4(time, baseAltitude, swell.Amplitude, swell.Wavelength));
            Shader.SetGlobalVector(Swell, new Vector4(swell.Speed, swell.Offset.x, swell.Offset.y, gridHalf));

            int count = Mathf.Min(holes.Count, ICloudHoles.MaxHoles);
            for (int i = 0; i < ICloudHoles.MaxHoles; i++)
            {
                CloudHoleShape hole = i < count ? holes[i] : default;
                HoleBuffer[i] = new Vector4(hole.Centre.x, hole.Centre.y, hole.Radius, hole.Rim);
                OpennessBuffer[i] = hole.Openness;
            }

            Shader.SetGlobalVectorArray(Holes, HoleBuffer);
            Shader.SetGlobalFloatArray(HoleOpenness, OpennessBuffer);
            Shader.SetGlobalFloat(HoleCount, count);
        }

        public static void Clear() => Shader.SetGlobalFloat(HoleCount, 0f);
    }
}
