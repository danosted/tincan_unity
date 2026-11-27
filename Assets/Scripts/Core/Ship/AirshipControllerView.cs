using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using System;
using System.Collections.Generic;
using Unity.Netcode;
using VContainer;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Infrastructure Layer: Physical implementation of an airship.
    /// Handles Rigidbody physics and acts as a moving platform for other actors.
    /// <para>
    /// Drawing on the host: the ship is simulated here at the tick rate, so for drawing only it is moved to a pose
    /// blended between ticks (<see cref="ShipDrawnPose"/>), as a client's interpolated ship is. It goes to the drawn pose
    /// in LateUpdate at order -75: after HumanoidPlayer (-100) carries and publishes players against the simulated pose,
    /// before the humanoid visuals (-50) and the camera (0) draw against it. It goes back to the simulated pose at the
    /// start of the next frame (the Initialization stage), before messages, physics and ticks read it.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DefaultExecutionOrder(-75)]
    public class AirshipControllerView : MonoBehaviour, IPointVelocityMovingGround, INetworkUpdateSystem
    {
        [Header("Handling")]
        [Tooltip("How this ship flies when no design says otherwise (a ship with no design). A designed ship flies on "
                 + "its core part's profile. Empty: ShipHandling.Default.")]
        [SerializeField] private ShipHandlingProfile _handlingProfile;

        [Header("Visual Hierarchy")]
        [SerializeField] private Transform _visualRoot;

        private Rigidbody _rb;
        private Vector3 _lastPosition;
        private Quaternion _lastRotation;
        private Vector3 _velocity;
        private Vector3 _positionDelta;
        private Quaternion _rotationDelta;
        private Vector3 _targetLinearVelocity;
        private Vector3 _targetAngularVelocity;

        private readonly ShipDrawnPose _drawnPose = new();
        private NetworkManager _network;
        private bool _showingDrawnPose;
        private Vector3 _simulatedPosition;
        private Quaternion _simulatedRotation;

        private ShipHandlingProfile _runtimeProfile;
        private ShipHandlingProfile _listeningTo;

        /// <summary>The profile the ship flies on: the one set at runtime (its design's core), else its prefab's; null: the default.</summary>
        public ShipHandlingProfile Profile => _runtimeProfile != null ? _runtimeProfile : _handlingProfile;

        /// <summary>What the ship flies on, read from <see cref="Profile"/> each time, so an edit to it applies at once.</summary>
        public ShipHandling Handling => Profile != null ? Profile.Handling : ShipHandling.Default;

        /// <summary>The handling changed: another profile was set, or the one it flies on was edited (tuned live).</summary>
        public event Action HandlingChanged;

        /// <summary>Fly on this profile from now on (the server, for a ship built from a design).</summary>
        public void SetHandling(ShipHandlingProfile profile)
        {
            _runtimeProfile = profile;
            if (isActiveAndEnabled) ListenTo(Profile);
            HandlingChanged?.Invoke();
        }

        private void OnEnable()
        {
            ListenTo(Profile);
            this.RegisterNetworkUpdate(NetworkUpdateStage.Initialization);
        }

        private void OnDisable()
        {
            ListenTo(null);
            this.UnregisterNetworkUpdate(NetworkUpdateStage.Initialization);
            RestoreSimulatedPose();
        }

        private void ListenTo(ShipHandlingProfile profile)
        {
            if (_listeningTo == profile) return;
            if (_listeningTo != null) _listeningTo.Changed -= OnProfileChanged;
            _listeningTo = profile;
            if (_listeningTo != null) _listeningTo.Changed += OnProfileChanged;
        }

        private void OnProfileChanged() => HandlingChanged?.Invoke();

        // Configuration
        public float MaxForwardSpeed => Handling.MaxForwardSpeed;
        public float MaxBackwardSpeed => Handling.MaxBackwardSpeed;
        public float AccelerationRate => Handling.AccelerationRate;
        public float DecelerationRate => Handling.DecelerationRate;
        public float AngularAcceleration => Handling.AngularAcceleration;
        public float AngularDeceleration => Handling.AngularDeceleration;
        public float VelocityBlendRate => Handling.VelocityBlendRate;
        public float TurnSpeed => Handling.TurnSpeed;
        public float PitchSpeed => Handling.PitchSpeed;
        public float MaxBankAngle => Handling.MaxBankAngle;
        public float BankSpeed => Handling.BankSpeed;
        public float PitchLevelSpeed => Handling.PitchLevelSpeed;
        public float ExternalPitchInfluence => Handling.ExternalPitchInfluence;

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
                _rb.interpolation = RigidbodyInterpolation.None; // Drawing is smoothed by ShipDrawnPose (host) or NetworkTransform (client)
                _rb.linearDamping = 0f;
                _rb.angularDamping = 0f;
                _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative; // Required for smooth kinematic pushing
            }

            _lastPosition = transform.position;
            _lastRotation = transform.rotation;
        }

        private void Update()
        {
            UpdateVisuals();
        }

        [Inject]
        public void Construct(NetworkManager network) => _network = network;

        /// <summary>The network tick clock: inside a tick that tick's exact time, between ticks the precise time now.</summary>
        private float Now => (float)_network.LocalTime.Time;

        /// <summary>Only a peer that both simulates and draws the ship (the host) smooths it; a dedicated server draws nothing.</summary>
        private bool DrawsSimulatedShip => _network != null && _network.IsListening && _network.IsHost;

        private void LateUpdate()
        {
            if (!DrawsSimulatedShip || !_drawnPose.IsActive(Now)) return;

            _simulatedPosition = transform.position;
            _simulatedRotation = transform.rotation;
            _drawnPose.Evaluate(_simulatedPosition, _simulatedRotation, Now, 1f / _network.NetworkConfig.TickRate,
                out var position, out var rotation);
            transform.SetPositionAndRotation(position, rotation);
            _showingDrawnPose = true;
        }

        public void NetworkUpdate(NetworkUpdateStage updateStage) => RestoreSimulatedPose();

        private void RestoreSimulatedPose()
        {
            if (!_showingDrawnPose) return;
            transform.SetPositionAndRotation(_simulatedPosition, _simulatedRotation);
            _showingDrawnPose = false;
        }

        public void Simulate(float deltaTime)
        {
            if (DrawsSimulatedShip) _drawnPose.BeginTick(transform.position, transform.rotation, Now);

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

        /// <summary>Put the ship there at rest: no movement this tick, so nothing standing on it is carried by the jump.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            RestoreSimulatedPose();
            transform.SetPositionAndRotation(position, rotation);
            _drawnPose.Snap(position, rotation);
            _targetLinearVelocity = Vector3.zero;
            _targetAngularVelocity = Vector3.zero;
            _velocity = Vector3.zero;
            _positionDelta = Vector3.zero;
            _rotationDelta = Quaternion.identity;
            _lastPosition = position;
            _lastRotation = rotation;
        }
    }
}
