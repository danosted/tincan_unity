#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Presentation Layer: the spherical compass at the helm (Assets/Prefabs/Navigation/Compass, placed by
    /// <see cref="CompassPresenter"/>). Inside a glass sphere:
    /// <list type="bullet">
    /// <item>the Disc is fixed to the ship, so it tilts and banks with the deck;</item>
    /// <item>the Arrow points at the active waypoint in 3D;</item>
    /// <item>the WaterLine always lies level with the world.</item>
    /// </list>
    /// On course means the arrow points straight out over the bow (the compass's +z) and lies in the disc. The math is
    /// static so it is tested without a scene.
    /// </summary>
    public class CompassView : MonoBehaviour
    {
        private const float PulseSeconds = 0.6f;
        private const float PulseScale = 0.35f;

        private Transform? _arrow;
        private Transform? _waterLine;
        private Vector3 _arrowScale = Vector3.one;
        private float _pulseLeft;
        private bool _aimed;

        public Transform? Arrow => _arrow;

        private void Awake()
        {
            _arrow = transform.FindDescendant("Arrow");
            _waterLine = transform.FindDescendant("WaterLine");
            if (_arrow != null) _arrowScale = _arrow.localScale;
            if (_arrow == null || _waterLine == null)
                Debug.LogWarning($"[{nameof(CompassView)}] {name} needs Arrow and WaterLine children.", this);
        }

        /// <summary>Every frame: point the arrow at <paramref name="target"/> (hidden without one) and level the water line.</summary>
        public void Show(Vector3? target, float turnSpeed, float deltaTime)
        {
            var parent = transform.rotation;
            if (_waterLine != null) _waterLine.localRotation = WaterLineLocalRotation(parent);
            if (_arrow == null) return;

            _arrow.gameObject.SetActive(target.HasValue);
            if (!target.HasValue)
            {
                _aimed = false;
                return;
            }

            var goal = ArrowLocalRotation(parent, transform.position, target.Value);
            _arrow.localRotation = _aimed ? Quaternion.RotateTowards(_arrow.localRotation, goal, turnSpeed * deltaTime) : goal;
            _aimed = true;

            _pulseLeft = Mathf.Max(0f, _pulseLeft - deltaTime);
            _arrow.localScale = _arrowScale * (1f + PulseScale * (_pulseLeft / PulseSeconds));
        }

        /// <summary>The route moved on: the arrow swells briefly as it swings to the new waypoint.</summary>
        public void Pulse() => _pulseLeft = PulseSeconds;

        /// <summary>The arrow's rotation in the compass's space to point its +z from <paramref name="from"/> at <paramref name="to"/>.</summary>
        public static Quaternion ArrowLocalRotation(Quaternion compassRotation, Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            if (direction.sqrMagnitude < 1e-6f) return Quaternion.identity;

            Vector3 up = Mathf.Abs(Vector3.Dot(direction.normalized, Vector3.up)) > 0.999f ? compassRotation * Vector3.forward : Vector3.up;
            return Quaternion.Inverse(compassRotation) * Quaternion.LookRotation(direction, up);
        }

        /// <summary>The water line's rotation in the compass's space so it lies level in the world, turned with the bow.</summary>
        public static Quaternion WaterLineLocalRotation(Quaternion compassRotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(compassRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.ProjectOnPlane(compassRotation * Vector3.up, Vector3.up);
            return Quaternion.Inverse(compassRotation) * Quaternion.LookRotation(forward, Vector3.up);
        }

        /// <summary>
        /// How far off course the ship is, in degrees: the angle between the bow (the compass's +z) and the direction to
        /// the target. Zero is dead on, in every direction (left/right and up/down).
        /// </summary>
        public static float OffCourseDegrees(Quaternion shipRotation, Vector3 from, Vector3 to) =>
            Vector3.Angle(shipRotation * Vector3.forward, to - from);
    }
}
