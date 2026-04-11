using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Features.Environment
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovingPlatform : MonoBehaviour, IMovingGround
    {
        private Rigidbody _rb;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;

        public Vector3 Velocity => _rb.linearVelocity;
        public Vector3 PositionDelta { get; private set; }
        public Quaternion RotationDelta { get; private set; }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _rb.isKinematic = true;
            _rb.interpolation = RigidbodyInterpolation.Interpolate; // Visual smoothness for high refresh rates

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private void Update()
        {
            // Calculate deltas based on frame-to-frame movement (Update)
            // instead of physics-to-physics movement (FixedUpdate).
            // This ensures the delta matches the frequency of the player's movement loop.
            PositionDelta = transform.position - _lastPosition;
            RotationDelta = transform.rotation * Quaternion.Inverse(_lastRotation);

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }
    }
}
