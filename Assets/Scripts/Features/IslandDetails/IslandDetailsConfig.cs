#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// What grows and lies on the sky islands, and how much (Assets/Settings/IslandDetails/IslandDetailsConfig, built by
    /// TinCan > Dev > Island Details > Build Assets). Plan: .docs/plans/island-life.md.
    /// </summary>
    [CreateAssetMenu(fileName = "IslandDetailsConfig", menuName = "TinCan/Environment/Island Details Config")]
    public class IslandDetailsConfig : ScriptableObject
    {
        [Header("Prefabs")]
        [Tooltip("Networked trees (a NetworkObject with a deadwood pile: the canopy's Log_* lobes are its firewood). One is picked per tree.")]
        public List<GameObject> TreePrefabs = new();
        [Tooltip("Boulders: scenery on every peer, solid. One is picked per boulder.")]
        public List<GameObject> BoulderPrefabs = new();
        [Tooltip("Bushes: scenery on every peer, not solid.")]
        public List<GameObject> BushPrefabs = new();

        [Header("Trees (main islands and landmasses)")]
        [Min(0f)] public float TreesPerHectare = 12f;
        [Tooltip("Most trees on a main island; each is a network object.")]
        [Min(0)] public int MaxTrees = 18;
        [Min(0)] public int MaxTreesOnALandmass = 70;
        [Tooltip("About one grove per this many hectares of top.")]
        [Min(0.05f)] public float GroveHectares = 0.6f;
        [Tooltip("How far a grove's trees stand from its middle, metres.")]
        [Min(1f)] public float GroveSpread = 14f;
        [Tooltip("Room each tree keeps, metres (times its scale).")]
        [Min(0.5f)] public float TreeSpacing = 4.5f;
        public Vector2 TreeScale = new(0.85f, 1.3f);

        [Header("Boulders and bushes (every island)")]
        [Min(0f)] public float BouldersPerHectare = 20f;
        [Min(0)] public int MaxBoulders = 80;
        [Min(0.5f)] public float BoulderSpacing = 3f;
        public Vector2 BoulderScale = new(0.6f, 1.7f);
        [Min(0f)] public float BushesPerHectare = 35f;
        [Min(0)] public int MaxBushes = 140;
        [Min(0.5f)] public float BushSpacing = 2f;
        public Vector2 BushScale = new(0.7f, 1.25f);

        [Header("Placement")]
        [Tooltip("Nothing stands further out than this share of the way to the rim.")]
        [Range(0.3f, 0.93f)] public float RimReach = 0.86f;

        [Header("Building")]
        [Tooltip("Seconds between the server's checks for islands that need trees or lost them.")]
        [Min(0.1f)] public float CheckIntervalSeconds = 0.5f;
        [Tooltip("Trees the server spawns per check at most; the rest wait for the next.")]
        [Min(1)] public int MaxTreeSpawnsPerCheck = 24;
        [Tooltip("Islands whose scenery is built per frame at most.")]
        [Min(1)] public int MaxSceneryBuildsPerFrame = 1;

        /// <summary>The placement's rules; <paramref name="clearingRadius"/> is the islands' own (SkyIslandConfig).</summary>
        public IslandDetailRules Rules(float clearingRadius) => new(TreesPerHectare, MaxTrees, MaxTreesOnALandmass,
            GroveHectares, GroveSpread, TreeSpacing, TreeScale, BouldersPerHectare, MaxBoulders, BoulderSpacing, BoulderScale,
            BushesPerHectare, MaxBushes, BushSpacing, BushScale, RimReach, clearingRadius, TreePrefabs.Count,
            BoulderPrefabs.Count, BushPrefabs.Count);
    }
}
