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

        [Header("Hazard zones (during a voyage)")]
        [Tooltip("A voyage lays hazard zones along its route (from its seed, the same on every peer, and on the navigator's " +
                 "chart). Nothing spawns in them since H0; comets (H1) may use them. Off: no zones.")]
        public bool ZonesEnabled = true;
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

        [Header("Ship contact")]
        [Tooltip("A hazard touches the ship when a sphere this big around it overlaps the ship's colliders.")]
        [Min(0.1f)] public float ContactRadius = 1.5f;
        [Tooltip("Instant effect applied to the ship on contact (GE_HazardImpact: -Health). The hazard is then removed.")]
        public GameplayEffectDefinition? ImpactEffect;

        [Header("Destruction")]
        [Tooltip("Seconds a destroyed hazard stays before it despawns (lets its last health update reach the clients).")]
        [Min(0f)] public float DespawnDelay = 0.5f;

        public HazardZoneRules ZoneRules => new(ZoneSpacing, ZoneRadius, ZoneLateralSpread, ZoneHalfHeight, ZoneAltitudeJitter, ZoneKeepClear);
    }
}
