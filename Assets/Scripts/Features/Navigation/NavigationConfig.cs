#nullable enable
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Tunables for navigation: routes, the compass at the helm and the chart table (Assets/Settings/Navigation/
    /// NavigationConfig, built by TinCan > Dev > Navigation > Build Assets). Plan: .docs/plans/navigation-compass.md.
    /// </summary>
    [CreateAssetMenu(fileName = "NavigationConfig", menuName = "TinCan/Navigation/Navigation Config")]
    public class NavigationConfig : ScriptableObject
    {
        [Header("Route")]
        [Tooltip("Most waypoints a route holds (its destination included). Keeps the replicated list small and the chart readable.")]
        [Min(1)] public int MaxWaypoints = 8;

        [Tooltip("A waypoint before the last counts as reached within this many metres (3D) of the ship; passing it also counts.")]
        [Min(1f)] public float WaypointRadius = 60f;

        [Header("Compass")]
        [Tooltip("The compass shown at every helm (presentation only; a plain prefab, no NetworkObject).")]
        public GameObject? CompassPrefab;

        [Tooltip("Where the compass sits, in the helm's own space (the helm faces +z; its wheel is at about y 1.75).")]
        public Vector3 CompassLocalPosition = new(1.1f, 2.05f, -0.15f);

        [Tooltip("Degrees per second the arrow turns toward a new heading, so a waypoint change swings it visibly.")]
        [Min(1f)] public float ArrowTurnSpeed = 240f;

        [Header("Chart")]
        [Tooltip("Metres from the ship to the chart's edge when a navigator sits down.")]
        [Min(100f)] public float ChartRange = 3000f;

        [Tooltip("The closest and widest the navigator can zoom the chart, in metres to its edge.")]
        public Vector2 ChartRangeLimits = new(500f, 8000f);

        [Tooltip("The chart's radius on the table, in metres of table.")]
        [Min(0.05f)] public float ChartRadius = 0.4f;

        [Tooltip("Table metres per metre of altitude difference: how high a pin stands for a waypoint above the ship.")]
        [Min(0f)] public float AltitudeScale = 0.0006f;

        [Tooltip("Chart cursor speed, in chart radii per unit of mouse delta.")]
        [Min(0f)] public float CursorSpeed = 0.0025f;

        [Tooltip("Chart radii within which a click picks a waypoint or a POI instead of adding a waypoint.")]
        [Min(0f)] public float PickRadius = 0.06f;

        [Tooltip("Metres per second a held Raise or Lower moves the selected waypoint.")]
        [Min(0f)] public float AltitudeRate = 60f;

        [Tooltip("Seconds between route updates sent while a waypoint is dragged or raised.")]
        [Min(0f)] public float SendInterval = 0.15f;

        [Tooltip("Materials for the chart's marks (URP; assets, so player builds include their shaders).")]
        public Material? ShipMarkMaterial;
        public Material? RouteMaterial;
        public Material? ActiveLegMaterial;
        public Material? PoiMaterial;
        public Material? RockMaterial;
        public Material? HazardMaterial;
        public Material? CursorMaterial;
    }
}
