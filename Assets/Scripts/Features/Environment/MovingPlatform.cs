using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Features.Environment
{
    [RequireComponent(typeof(Rigidbody))]
    public class MovingPlatform : MonoBehaviour, IMovingPlatform
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
            _rb.isKinematic = true; // Essential for moving platforms
            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            // Calculate deltas based on physics update
            PositionDelta = transform.position - _lastPosition;
            RotationDelta = transform.rotation * Quaternion.Inverse(_lastRotation);

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }
    }
}
