using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using TinCan.Features.Airship;
using System;
using System.Collections.Generic;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Infrastructure Layer: Physical implementation of an airship.
    /// Handles Rigidbody physics and acts as a moving platform for other actors.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class AirshipControllerView : ControllableActorBase, IPossessionReceiver, IMovingGround
    {
        [Header("Movement Settings")]
        [SerializeField] private float _maxForwardSpeed = 15f;
        [SerializeField] private float _maxBackwardSpeed = 8f;
        [SerializeField] private float _accelerationRate = 5f;
        [SerializeField] private float _decelerationRate = 2f;
        [SerializeField] private float _turnSpeed = 45f;
        [SerializeField] private float _pitchSpeed = 30f;
        private Rigidbody _rb;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _velocity;
        private Vector3 _positionDelta;
        private Quaternion _rotationDelta;


        // Configuration
        public float MaxForwardSpeed => _maxForwardSpeed;
        public float MaxBackwardSpeed => _maxBackwardSpeed;
        public float AccelerationRate => _accelerationRate;
        public float DecelerationRate => _decelerationRate;
        public float TurnSpeed => _turnSpeed;
        public float PitchSpeed => _pitchSpeed;

        // IMovingGround data
        public Vector3 Velocity => _velocity;
        public Vector3 PositionDelta => _positionDelta;
        public Quaternion RotationDelta => _rotationDelta;

        protected void Awake()
        {
            _rb = GetComponent<Rigidbody>();

            if (_rb != null)
            {
                _rb.useGravity = false;
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
                _rb.linearDamping = 0f;
                _rb.angularDamping = 0f;
                _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            }

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private void FixedUpdate()
        {
            // Calculate Deltas for IMovingGround consumers (players on deck)
            _positionDelta = transform.position - _lastPosition;
            _rotationDelta = transform.rotation * Quaternion.Inverse(_lastRotation);

            float dt = Time.fixedDeltaTime;
            _velocity = dt > 0 ? _positionDelta / dt : Vector3.zero;

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        public void ApplyMovement(Vector3 velocity, Vector3 angularVelocity)
        {
            if (_rb == null) return;
            Debug.Log($"[AirshipControllerView] Applying movement. Velocity: {velocity}, AngularVelocity: {angularVelocity}");
            _rb.linearVelocity = velocity;
            _rb.angularVelocity = angularVelocity;
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
