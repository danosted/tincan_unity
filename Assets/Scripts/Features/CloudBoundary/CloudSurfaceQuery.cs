#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    public interface ICloudSurfaceQuery
    {
        float GetSurfaceHeight(float worldX, float worldZ);
    }

    public class CloudSurfaceQuery : ICloudSurfaceQuery
    {
        private readonly CloudBoundaryConfig _config;

        public CloudSurfaceQuery(CloudBoundaryConfig config)
        {
            _config = config;
        }

        public float GetSurfaceHeight(float worldX, float worldZ)
        {
            if (_config.Topology == CloudTopology.Flat)
            {
                return _config.BaseAltitude;
            }

            float seedX = (_config.WorldSeed & 0xFFFF) * 0.0137f;
            float seedZ = ((_config.WorldSeed >> 16) & 0xFFFF) * 0.0173f;
            float x = worldX * _config.HeightFrequency + seedX;
            float z = worldZ * _config.HeightFrequency + seedZ;
            float height = SignedPerlin(x, z);

            if (_config.Topology == CloudTopology.Layered)
            {
                height = (height * 0.7f) + (SignedPerlin(x * 2.03f + 17f, z * 2.03f - 29f) * 0.3f);
            }

            return _config.BaseAltitude + height * _config.HeightAmplitude;
        }

        private static float SignedPerlin(float x, float z) => Mathf.PerlinNoise(x, z) * 2f - 1f;
    }
}
