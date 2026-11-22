#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>The sites' tunables, from <see cref="SkyIslandConfig"/>. Distances in metres.</summary>
    public readonly struct SkyIslandSiteRules
    {
        public readonly float CaveChance;
        public readonly float CaveMinIslandRadius;
        public readonly Vector2Int LandmassCaves;
        public readonly Vector2 CaveRadius;
        public readonly Vector2 CaveHeight;
        public readonly float CaveWall;
        public readonly float CaveMaxGroundRise;
        public readonly Vector2Int LandmassClearings;
        public readonly float ClearingRadius;

        public SkyIslandSiteRules(float caveChance, float caveMinIslandRadius, Vector2Int landmassCaves, Vector2 caveRadius,
            Vector2 caveHeight, float caveWall, float caveMaxGroundRise, Vector2Int landmassClearings, float clearingRadius)
        {
            CaveChance = caveChance;
            CaveMinIslandRadius = caveMinIslandRadius;
            LandmassCaves = landmassCaves;
            CaveRadius = caveRadius;
            CaveHeight = caveHeight;
            CaveWall = caveWall;
            CaveMaxGroundRise = caveMaxGroundRise;
            LandmassClearings = landmassClearings;
            ClearingRadius = clearingRadius;
        }
    }
}
