#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>The layout's tunables, from <see cref="SkyIslandConfig"/>. Distances in metres, world space.</summary>
    public readonly struct SkyIslandLayoutRules
    {
        public readonly float CellSize;
        public readonly float MainChance;
        public readonly Vector2 MainRadius;
        public readonly Vector2 DepthPerRadius;
        public readonly Vector2 TopAltitude;
        public readonly Vector2Int SatellitesPerMain;
        public readonly float LoneSatelliteChance;
        public readonly Vector2Int LoneSatellites;
        public readonly Vector2 SatelliteRadius;
        public readonly Vector2 SatelliteSpread;
        public readonly float SatelliteAltitudeJitter;
        public readonly float KeepOutRadius;

        public SkyIslandLayoutRules(float cellSize, float mainChance, Vector2 mainRadius, Vector2 depthPerRadius,
            Vector2 topAltitude, Vector2Int satellitesPerMain, float loneSatelliteChance, Vector2Int loneSatellites,
            Vector2 satelliteRadius, Vector2 satelliteSpread, float satelliteAltitudeJitter, float keepOutRadius)
        {
            CellSize = cellSize;
            MainChance = mainChance;
            MainRadius = mainRadius;
            DepthPerRadius = depthPerRadius;
            TopAltitude = topAltitude;
            SatellitesPerMain = satellitesPerMain;
            LoneSatelliteChance = loneSatelliteChance;
            LoneSatellites = loneSatellites;
            SatelliteRadius = satelliteRadius;
            SatelliteSpread = satelliteSpread;
            SatelliteAltitudeJitter = satelliteAltitudeJitter;
            KeepOutRadius = keepOutRadius;
        }

        /// <summary>
        /// How far a main island and its satellites can reach from the main island's centre. Centres stay this far
        /// inside their cell, so islands in neighbouring cells never touch.
        /// </summary>
        public float ClusterReach => MainRadius.y * SatelliteSpread.y + 2f * SatelliteRadius.y;
    }
}
