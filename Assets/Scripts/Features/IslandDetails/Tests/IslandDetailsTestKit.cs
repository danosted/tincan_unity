#nullable enable
using TinCan.Features.SkyIslands;
using UnityEngine;

namespace TinCan.Features.IslandDetails.Tests
{
    /// <summary>Islands and configs for the island details tests.</summary>
    public static class IslandDetailsTestKit
    {
        /// <summary>A config with three tree, two boulder and two bush variants (placeholders: placement only counts them).</summary>
        public static IslandDetailsConfig Config()
        {
            var config = ScriptableObject.CreateInstance<IslandDetailsConfig>();
            for (int i = 0; i < 3; i++) config.TreePrefabs.Add(null!);
            for (int i = 0; i < 2; i++)
            {
                config.BoulderPrefabs.Add(null!);
                config.BushPrefabs.Add(null!);
            }
            return config;
        }

        public static SkyIslandSpec Main(uint shapeSeed, float radius = 70f, int cellX = 0) =>
            new(new SkyIslandId(1, cellX, (int)shapeSeed, 0), new Vector3(cellX * 450f, 30f, shapeSeed * 450f), radius, radius * 1.2f, shapeSeed);

        public static SkyIslandSpec Satellite(uint shapeSeed) =>
            new(new SkyIslandId(1, 0, (int)shapeSeed, 2), new Vector3(100f, 20f, shapeSeed * 450f), 12f, 18f, shapeSeed);

        public static SkyIslandSpec Landmass(uint shapeSeed) =>
            new(new SkyIslandId(1, 7, (int)shapeSeed, 0), new Vector3(3150f, 30f, shapeSeed * 450f), 180f, 120f, shapeSeed, isLandmass: true);
    }
}
