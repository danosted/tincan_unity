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
    public class AirshipControllerView : MonoBehaviour, IControllable, IPossessionReceiver, IPointVelocityMovingGround, IHasOrbitalCamera
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

        [Header("Visual Hierarchy")]
        [SerializeField] private Transform _visualRoot;

        private ThirdPersonLookView _look;
        private Rigidbody _rb;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _velocity;
        private Vector3 _positionDelta;
        private Quaternion _rotationDelta;
        private Vector3 _targetLinearVelocity;
        private Vector3 _targetAngularVelocity;

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

        public Vector3 GetPointVelocity(Vector3 worldPoint)
        {
            Vector3 angularVelocityRadians = _targetAngularVelocity * Mathf.Deg2Rad;
            return _velocity + Vector3.Cross(angularVelocityRadians, worldPoint - transform.position);
        }

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

            _look = GetComponent<ThirdPersonLookView>();
        }

        private void Update()
        {
            UpdateVisuals();
        }

        public void Simulate(float deltaTime)
        {
            if (_rb != null && _rb.isKinematic)
            {
                Vector3 deltaPosition = _targetLinearVelocity * deltaTime;
                Quaternion deltaRotation = Quaternion.Euler(_targetAngularVelocity * deltaTime);

                transform.position += deltaPosition;
                transform.rotation = transform.rotation * deltaRotation;
            }

            _positionDelta = transform.position - _lastPosition;
            _rotationDelta = transform.rotation * Quaternion.Inverse(_lastRotation);
            _velocity = _targetLinearVelocity;

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private void UpdateVisuals()
        {
            if (_visualRoot != null)
            {
                _visualRoot.localPosition = Vector3.zero;
                _visualRoot.localRotation = Quaternion.identity;
            }
        }

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
