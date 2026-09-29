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
        [Min(0)] public int MaxAlive = 6;
        [Tooltip("The box hazards appear in, in metres in the ship's level heading frame: x starboard, y up, z ahead. " +
                 "Keep it inside the cannons' arcs (the first cannon points starboard).")]
        public Vector3 FieldMin = new(30f, -10f, -20f);
        public Vector3 FieldMax = new(110f, 25f, 120f);
        [Tooltip("A hazard this far from the ship is removed (and replaced).")]
        [Min(1f)] public float RemoveDistance = 260f;
        [Tooltip("Seconds between spawns while the field is below MaxAlive.")]
        [Min(0f)] public float SpawnInterval = 1.5f;

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
    }
}
