#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using UnityEngine;
using VContainer;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// The cloud surface's height at a point: the topology's shape, then the drop into any open hole (when the Cloud Sea
    /// feature is installed). With <see cref="CloudTopology.Swell"/> it is the same formula the drawn sea uses
    /// (<see cref="CloudSeaFormula"/>), on the shared clock.
    /// </summary>
    public class CloudSurfaceQuery : ICloudSurfaceQuery
    {
        private readonly CloudBoundaryConfig _config;
        private readonly ITimeService? _time;
        private readonly IReadOnlyList<ICloudHoles> _holes;

        public CloudSurfaceQuery(CloudBoundaryConfig config) : this(config, null, Array.Empty<ICloudHoles>())
        {
        }

        [Inject]
        public CloudSurfaceQuery(CloudBoundaryConfig config, ITimeService? time, IReadOnlyList<ICloudHoles> holes)
        {
            _config = config;
            _time = time;
            _holes = holes;
        }

        public float GetSurfaceHeight(float worldX, float worldZ)
        {
            float height = TopologyHeight(worldX, worldZ);
            if (_holes.Count == 0) return height;

            return height - CloudSeaFormula.HoleCut(worldX, worldZ, _holes[0].ShapesNow) * _config.HoleDepth;
        }

        private float TopologyHeight(float worldX, float worldZ)
        {
            switch (_config.Topology)
            {
                case CloudTopology.Flat:
                    return _config.BaseAltitude;
                case CloudTopology.Swell:
                    return _config.BaseAltitude + CloudSeaFormula.Swell(worldX, worldZ, _time?.Time ?? 0f, _config.Swell);
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
