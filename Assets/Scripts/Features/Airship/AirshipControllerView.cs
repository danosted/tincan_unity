using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using TinCan.Features.Airship;
using System;
using System.Collections.Generic;
using TinCan.Features.HumanoidMovement;
using TinCan.Features.FreeCamera;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Infrastructure Layer: Physical implementation of an airship.
    /// Handles Rigidbody physics and acts as a moving platform for other actors.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(ThirdPersonLookView))]
    public class AirshipControllerView : MonoBehaviour, IControllable, IPossessionReceiver, IMovingGround, IHasOrbitalCamera
    {
        [Header("Movement Settings")]
        [SerializeField] private float _maxForwardSpeed = 15f;
        [SerializeField] private float _maxBackwardSpeed = 8f;
        [SerializeField] private float _accelerationRate = 5f;
        [SerializeField] private float _decelerationRate = 2f;
        [SerializeField] private float _angularAcceleration = 15f;
        [SerializeField] private float _angularDeceleration = 20f;
        [SerializeField] private float _velocityBlendRate = 2f;
        [SerializeField] private float _turnSpeed = 45f;
        [SerializeField] private float _pitchSpeed = 30f;
        [SerializeField] private float _maxBankAngle = 15f;
        [SerializeField] private float _bankSpeed = 2f;

        [Header("Visual Smoothing")]
        [SerializeField] private Transform _visualRoot;
        [SerializeField] private float _visualPositionSmoothSpeed = 15f;
        [SerializeField] private float _visualRotationSmoothSpeed = 15f;

        private ThirdPersonLookView _look;
        private Rigidbody _rb;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _velocity;
        private Vector3 _positionDelta;
        private Quaternion _rotationDelta;
        private Vector3 _targetLinearVelocity;
        private Vector3 _targetAngularVelocity;

        private Vector3 _visualPosition;
        private Quaternion _visualRotation;
        private Vector3 _lastVisualPosition;
        private Quaternion _lastVisualRotation;

        public bool IsControlsEnabled { get; private set; } = false;

        // Implement IActor required by IHasOrbitalCamera. For a NetworkBehaviour this would usually come from the base class.
        // Assuming this is a simple MonoBehaviour for now, we'll provide an ID.
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;

        public IOrbitalLookView Look => _look;

        public void DisableControls()
        {
            IsControlsEnabled = false;
        }

        public void EnableControls()
        {
            IsControlsEnabled = true;
        }

        // Configuration
        public float MaxForwardSpeed => _maxForwardSpeed;
        public float MaxBackwardSpeed => _maxBackwardSpeed;
        public float AccelerationRate => _accelerationRate;
        public float DecelerationRate => _decelerationRate;
        public float AngularAcceleration => _angularAcceleration;
        public float AngularDeceleration => _angularDeceleration;
        public float VelocityBlendRate => _velocityBlendRate;
        public float TurnSpeed => _turnSpeed;
        public float PitchSpeed => _pitchSpeed;
        public float MaxBankAngle => _maxBankAngle;
        public float BankSpeed => _bankSpeed;

        // IMovingGround data
        public Vector3 Velocity => _velocity;
        public Vector3 PositionDelta => _positionDelta;
        public Quaternion RotationDelta => _rotationDelta;

        protected void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (_rb != null)
            {
                _rb.isKinematic = true; // The fix: Make the ship immune to physics pushing
                _rb.useGravity = false;
                _rb.interpolation = RigidbodyInterpolation.None; // Manual smoothing handles this now
                _rb.linearDamping = 0f;
                _rb.angularDamping = 0f;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // Required for smooth kinematic pushing
            }

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;

            _visualPosition = transform.position;
            _visualRotation = transform.rotation;
            _lastVisualPosition = transform.position;
            _lastVisualRotation = transform.rotation;

            _look = GetComponent<ThirdPersonLookView>();
        }

        private void Update()
        {
            // 1. Simulation (The "Instant" truth)
            if (_rb != null && _rb.isKinematic)
            {
                Vector3 deltaPosition = _targetLinearVelocity * Time.deltaTime;
                Quaternion deltaRotation = Quaternion.Euler(_targetAngularVelocity * Time.deltaTime);

                transform.position += deltaPosition;
                transform.rotation = transform.rotation * deltaRotation;
            }

            // 2. Smoothing (Applied to visuals + colliders child)
            Transform syncSource = _visualRoot != null ? _visualRoot : transform;

            if (_visualRoot != null)
            {
                _visualPosition = Vector3.Lerp(_visualPosition, transform.position, Time.deltaTime * _visualPositionSmoothSpeed);
                _visualRotation = Quaternion.Slerp(_visualRotation, transform.rotation, Time.deltaTime * _visualRotationSmoothSpeed);

                _visualRoot.position = _visualPosition;
                _visualRoot.rotation = _visualRotation;
            }

            // 3. Calculate Deltas from the smoothed surface
            // This ensures players standing on the deck follow the smooth movement, not the "snap"
            _positionDelta = syncSource.position - _lastVisualPosition;
            _rotationDelta = syncSource.rotation * Quaternion.Inverse(_lastVisualRotation);

            // We use _targetLinearVelocity for logical velocity so it's consistent for momentum
            _velocity = _targetLinearVelocity;

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
            _lastVisualPosition = syncSource.position;
            _lastVisualRotation = syncSource.rotation;
        }

        private void LateUpdate()
        {
            // Visual logic moved to Update to ensure IMovingGround deltas are ready for CharacterControllers
        }

        /* FixedUpdate removed to prevent Update/FixedUpdate desync with CharacterController */

        public void ApplyMovement(Vector3 linearVelocity, Vector3 angularVelocity)
        {
            _targetLinearVelocity = linearVelocity;
            _targetAngularVelocity = angularVelocity;
        }

        public void OnPossessed(ulong playerId)
        {
            EnableControls();
        }

        public void OnUnpossessed()
        {
            DisableControls();
        }
    }
}
