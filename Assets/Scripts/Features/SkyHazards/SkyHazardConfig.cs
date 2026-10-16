#nullable enable
using TinCan.Core.Gas;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>Tunables for sky hazards (Assets/Settings/SkyHazards/SkyHazardConfig). Plan: .docs/plans/cannon-and-hazards.md.</summary>
    [CreateAssetMenu(fileName = "SkyHazardConfig", menuName = "TinCan/Sky Hazards/Sky Hazard Config")]
    public class SkyHazardConfig : ScriptableObject
    {
        [Tooltip("Networked hazard prefab: NetworkObject, EntityNetworkMediator, AbilityNetworkMediator, SkyHazardNetworkMediator, a collider.")]
        public GameObject? Prefab;

        [Header("Field around the ship")]
        [Tooltip("Keep a field of hazards around the ship. Off: they only appear on demand (scenarios, events).")]
        public bool FieldEnabled;
        [Tooltip("Hazards alive at once with one player aboard.")]
        [Min(0)] public int MaxAlive = 6;
        [Tooltip("Hazards added to MaxAlive for each player beyond the first.")]
        [Min(0)] public int MaxAlivePerExtraPlayer = 2;
        [Tooltip("The box hazards appear in, in metres in the ship's level heading frame: x starboard, y up, z ahead. " +
                 "Ahead of the bow: the foredeck cannons, one on each side, swing across it.")]
        public Vector3 FieldMin = new(-45f, -10f, 90f);
        public Vector3 FieldMax = new(45f, 25f, 170f);
        [Tooltip("A hazard this far from the ship is removed (and replaced).")]
        [Min(1f)] public float RemoveDistance = 260f;
        [Tooltip("Seconds between spawns while the field is below its limit, with one player aboard.")]
        [Min(0f)] public float SpawnInterval = 1.5f;
        [Tooltip("Each player beyond the first multiplies the spawn interval by this (below 1: a bigger crew, faster spawns).")]
        [Range(0.1f, 1f)] public float SpawnIntervalScalePerExtraPlayer = 0.8f;

        [Header("Hazard zones (during a voyage)")]
        [Tooltip("A voyage lays zones of thick hazards along its route (from its seed, the same on every peer, and on the " +
                 "navigator's chart). Off, or before any voyage: the field is the same everywhere.")]
        public bool ZonesEnabled = true;
        [Tooltip("Outside every zone the field keeps this share of its hazards, and spawns this much slower.")]
        [Range(0f, 1f)] public float OutsideZoneScale = 0.35f;
        [Tooltip("Metres along the route between zones.")]
        [Min(100f)] public float ZoneSpacing = 900f;
        [Tooltip("Radius range of a zone, in metres (level).")]
        public Vector2 ZoneRadius = new(220f, 400f);
        [Tooltip("A zone's centre sits up to this many metres to either side of the route.")]
        [Min(0f)] public float ZoneLateralSpread = 250f;
        [Tooltip("Half the height of a zone's band, in metres.")]
        public Vector2 ZoneHalfHeight = new(50f, 110f);
        [Tooltip("A zone's middle sits up to this many metres above or below the route.")]
        [Min(0f)] public float ZoneAltitudeJitter = 40f;
        [Tooltip("No zone comes within this many metres (beyond its radius) of the voyage's start or destination.")]
        [Min(0f)] public float ZoneKeepClear = 300f;

        [Header("Drift and ship contact")]
        [Tooltip("Metres per second a field hazard homes on the ship. 0: field hazards hang still. Targets placed with " +
                 "SpawnAt stay still unless asked to drift.")]
        [Min(0f)] public float DriftSpeed = 4f;
        [Tooltip("A hazard touches the ship when a sphere this big around it overlaps the ship's colliders.")]
        [Min(0.1f)] public float ContactRadius = 1.5f;
        [Tooltip("Instant effect applied to the ship on contact (GE_HazardImpact: -Health). The hazard is then removed.")]
        public GameplayEffectDefinition? ImpactEffect;

        [Header("Destruction")]
        [Tooltip("Seconds a destroyed hazard stays before it despawns (lets its last health update reach the clients).")]
        [Min(0f)] public float DespawnDelay = 0.5f;

        public SkyHazardFieldShape FieldShape => new(FieldMin, FieldMax);
        public SkyHazardCrewScaling CrewScaling => new(MaxAlive, MaxAlivePerExtraPlayer, SpawnInterval, SpawnIntervalScalePerExtraPlayer);
        public HazardZoneRules ZoneRules => new(ZoneSpacing, ZoneRadius, ZoneLateralSpread, ZoneHalfHeight, ZoneAltitudeJitter, ZoneKeepClear);
    }
}
