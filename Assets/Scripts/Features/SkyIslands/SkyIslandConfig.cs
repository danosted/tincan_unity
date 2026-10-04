#nullable enable
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>Tunables for sky islands (Assets/Settings/SkyIslands/SkyIslandConfig). Plan: .docs/plans/sky-islands.md.</summary>
    [CreateAssetMenu(fileName = "SkyIslandConfig", menuName = "TinCan/Environment/Sky Island Config")]
    public class SkyIslandConfig : ScriptableObject
    {
        [Header("Layout")]
        [Tooltip("The layout seed while no voyage has begun (or without the Voyage feature). A voyage brings its own.")]
        public int WorldSeed = 1;
        [Tooltip("Side of one layout cell, in metres. Each cell holds at most one island cluster.")]
        [Min(50f)] public float CellSize = 450f;
        [Tooltip("Chance that a cell holds a main island.")]
        [Range(0f, 1f)] public float MainChance = 0.55f;
        [Tooltip("Radius range of a main island's top, in metres.")]
        public Vector2 MainRadius = new(35f, 95f);
        [Tooltip("How deep the rock hangs below the top, as a multiple of the radius.")]
        public Vector2 DepthPerRadius = new(0.9f, 1.6f);
        [Tooltip("World altitude range of a main island's top. The ship spawns at about 40 m and cruises near it.")]
        public Vector2 TopAltitude = new(0f, 80f);

        [Header("Satellites")]
        [Tooltip("Small rocks around a main island.")]
        public Vector2Int SatellitesPerMain = new(1, 4);
        [Tooltip("Chance that a cell without a main island holds a few lone small rocks.")]
        [Range(0f, 1f)] public float LoneSatelliteChance = 0.35f;
        public Vector2Int LoneSatellites = new(1, 3);
        [Tooltip("Radius range of a small rock, in metres.")]
        public Vector2 SatelliteRadius = new(5f, 14f);
        [Tooltip("How far a satellite sits from its main island's centre, as a multiple of the main island's radius.")]
        public Vector2 SatelliteSpread = new(1.25f, 1.7f);
        [Tooltip("Satellites sit up to this many metres above or below their main island's top.")]
        [Min(0f)] public float SatelliteAltitudeJitter = 30f;

        [Header("Keep clear")]
        [Tooltip("No island comes within this many metres (level) of the voyage's start or destination, or of the world " +
                 "origin before any voyage, so the ship never starts or arrives inside rock.")]
        [Min(0f)] public float KeepOutRadius = 160f;

        [Header("Impact")]
        [Tooltip("Instant effect on the ship when it hits an island (GE_IslandImpact: -Health). The ship is pushed out either way.")]
        public GameplayEffectDefinition? ImpactEffect;
        [Tooltip("Seconds before island rock can hurt the ship again; scraping along an island hurts once per this.")]
        [Min(0f)] public float ImpactCooldown = 1.5f;

        [Header("Streaming")]
        [Tooltip("Islands exist in the cells within this many metres of the ship's cell. The player camera draws to 1000 m.")]
        [Min(100f)] public float StreamRadius = 1000f;
        [Tooltip("Islands built per frame at most; the rest wait for the next frames.")]
        [Min(1)] public int MaxBuildsPerFrame = 2;

        [Header("Meshes")]
        public SkyIslandMeshResolution MainVisual = new(48, 6, 14);
        public SkyIslandMeshResolution MainCollision = new(16, 2, 5);
        public SkyIslandMeshResolution SatelliteVisual = new(20, 2, 6);
        public SkyIslandMeshResolution SatelliteCollision = new(10, 1, 3);
        [Tooltip("Collision is cut into convex wedges this many segments wide (more: a closer fit, more colliders).")]
        [Min(3)] public int CollisionWedgeSegments = 4;
        [Tooltip("Material for the visual mesh (Shaders/SkyIsland). Peers without graphics build collision only.")]
        public Material? Material;

        public SkyIslandLayoutRules LayoutRules => new(CellSize, MainChance, MainRadius, DepthPerRadius, TopAltitude,
            SatellitesPerMain, LoneSatelliteChance, LoneSatellites, SatelliteRadius, SatelliteSpread,
            SatelliteAltitudeJitter, KeepOutRadius);

        private void OnValidate()
        {
            float reach = LayoutRules.ClusterReach;
            if (2f * reach >= CellSize)
                Debug.LogWarning($"[{name}] A cluster reaches {reach:0} m from its centre; cells of {CellSize:0} m " +
                                 "are too small to keep clusters in neighbouring cells apart.", this);
        }
    }
}
