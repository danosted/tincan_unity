#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudFlight
{
    /// <summary>
    /// The knobs of the cloud flight (plan .docs/plans/cloud-sea.md, "Soaring and cloud shelves"): how a dive builds
    /// speed, how a climb spends it, how the cloud carries a ship and how the pull builds off it, and where the sky thins.
    /// </summary>
    [CreateAssetMenu(fileName = "CloudFlightConfig", menuName = "TinCan/Environment/Cloud Flight Config")]
    public class CloudFlightConfig : ScriptableObject
    {
        [Header("Where the cloud is")]
        [Tooltip("How far above the cloud surface a ship's origin counts as in the cloud, in metres: where a slow ship " +
                 "comes to rest, its hull on the drawn tops and its deck clear of the fog.")]
        [SerializeField] private float _restHeight = 25f;
        [Tooltip("Metres over which a ship goes from open air to fully in the cloud, so the change is never a step.")]
        [SerializeField, Min(0.1f)] private float _cloudBlend = 4f;

        [Header("Speed and height")]
        [Tooltip("How hard the nose's slope pulls on the speed, m/s² at straight down (gravity along the path).")]
        [SerializeField, Min(0f)] private float _pull = 9.8f;
        [Tooltip("In the cloud, how much more a dive builds speed than in open air (the cloud carries you).")]
        [SerializeField, Min(0f)] private float _diveGain = 2.5f;
        [Tooltip("The steepest climb the engine holds for free, in degrees: steeper climbs spend momentum.")]
        [SerializeField, Range(0f, 45f)] private float _freeClimb = 10f;
        [Tooltip("In the cloud, how much of a climb's cost is paid (0: climbing in cloud is free, 1: as in open air).")]
        [SerializeField, Range(0f, 1f)] private float _climbCost = 0.2f;
        [Tooltip("How fast momentum fades in open air, per second (low: a launch carries far).")]
        [SerializeField, Min(0f)] private float _airDrag = 0.02f;
        [Tooltip("How fast momentum fades in the cloud, per second.")]
        [SerializeField, Min(0f)] private float _cloudDrag = 0.03f;
        [Tooltip("The fastest a ship flies with its momentum, m/s.")]
        [SerializeField, Min(1f)] private float _maxSpeed = 90f;

        [Header("Riding the sea")]
        [Tooltip("How hard the cloud carries a ship back to its rest height, m/s per metre off it: the ship rides the " +
                 "swell, and the deeper it is pushed in, the harder the cloud pushes it out.")]
        [SerializeField, Min(0f)] private float _buoyancy = 1f;
        [Tooltip("The most the cloud lifts or settles a ship, m/s.")]
        [SerializeField, Min(0f)] private float _maxHeave = 10f;
        [Tooltip("How fast the cloud takes away a fall when a ship comes back to it, m/s².")]
        [SerializeField, Min(0f)] private float _cloudCatch = 15f;

        [Header("Off the sea")]
        [Tooltip("Off the cloud there is no updraft: the pull towards the sea builds this fast, m/s².")]
        [SerializeField, Min(0f)] private float _gravity = 4f;
        [Tooltip("The fastest a ship falls at speed, m/s (the glide).")]
        [SerializeField, Min(0f)] private float _glideFall = 5f;
        [Tooltip("The fastest a stalled ship falls, m/s.")]
        [SerializeField, Min(0f)] private float _stallFall = 15f;
        [Tooltip("Below this speed a ship in open air starts to stall, m/s.")]
        [SerializeField, Min(0.1f)] private float _stallSpeed = 8f;
        [Tooltip("How fast a stalled ship's raised nose drops, degrees per second (it never drops below level).")]
        [SerializeField, Min(0f)] private float _noseDrop = 12f;

        [Header("The soft ceiling")]
        [Tooltip("Height above the rest height where the air starts to thin, in metres.")]
        [SerializeField, Min(0f)] private float _ceilingStart = 400f;
        [Tooltip("Metres over which the thinning reaches its full sink.")]
        [SerializeField, Min(1f)] private float _ceilingDepth = 300f;
        [Tooltip("The extra fall where the air is fully thin, m/s.")]
        [SerializeField, Min(0f)] private float _ceilingSink = 10f;

        [Header("Developer")]
        [Tooltip("Show the cloud flight dashboard, top right (the host's numbers for the first ship). Live.")]
        [SerializeField] private bool _showDashboard = true;

        public float RestHeight { get => _restHeight; set => _restHeight = value; }
        public float CloudBlend { get => _cloudBlend; set => _cloudBlend = value; }
        public float Pull { get => _pull; set => _pull = value; }
        public float DiveGain { get => _diveGain; set => _diveGain = value; }
        public float FreeClimb { get => _freeClimb; set => _freeClimb = value; }
        public float ClimbCost { get => _climbCost; set => _climbCost = value; }
        public float AirDrag { get => _airDrag; set => _airDrag = value; }
        public float CloudDrag { get => _cloudDrag; set => _cloudDrag = value; }
        public float MaxSpeed { get => _maxSpeed; set => _maxSpeed = value; }
        public float Buoyancy { get => _buoyancy; set => _buoyancy = value; }
        public float MaxHeave { get => _maxHeave; set => _maxHeave = value; }
        public float CloudCatch { get => _cloudCatch; set => _cloudCatch = value; }
        public float Gravity { get => _gravity; set => _gravity = value; }
        public float GlideFall { get => _glideFall; set => _glideFall = value; }
        public float StallFall { get => _stallFall; set => _stallFall = value; }
        public float StallSpeed { get => _stallSpeed; set => _stallSpeed = value; }
        public float NoseDrop { get => _noseDrop; set => _noseDrop = value; }
        public float CeilingStart { get => _ceilingStart; set => _ceilingStart = value; }
        public float CeilingDepth { get => _ceilingDepth; set => _ceilingDepth = value; }
        public float CeilingSink { get => _ceilingSink; set => _ceilingSink = value; }
        public bool ShowDashboard { get => _showDashboard; set => _showDashboard = value; }
    }
}
