using Unity.Netcode;
using UnityEngine;
using TinCan.Features.Airship;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using System;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Bridges the Airship logic with Netcode for GameObjects.
    /// Handles synchronization of steering input and possession state.
    /// </summary>
    [RequireComponent(typeof(AirshipControllerView))]
    public class AirshipNetworkMediator : NetworkMediator, IAirshipView
    {
        private AirshipControllerView _view;

        private readonly NetworkVariable<AirshipInputState> _netInputState = new NetworkVariable<AirshipInputState>(
            writePerm: NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<ulong> _netOwnerId = new NetworkVariable<ulong>(ulong.MaxValue);
        private readonly NetworkVariable<ulong> _netPossessorId = new NetworkVariable<ulong>(ulong.MaxValue);

        private readonly NetworkVariable<Vector3> _netPosition = new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>(
            writePerm: NetworkVariableWritePermission.Owner);

        // IAirshipView Implementation (Forwarding to view)
        public Transform Transform => _view.transform;
        public float MaxForwardSpeed => _view.MaxForwardSpeed;
        public float MaxBackwardSpeed => _view.MaxBackwardSpeed;
        public float AccelerationRate => _view.AccelerationRate;
        public float DecelerationRate => _view.DecelerationRate;
        public float TurnSpeed => _view.TurnSpeed;
        public float PitchSpeed => _view.PitchSpeed;

        public bool IsSimulating => true;
        public Guid Id { get; } = Guid.NewGuid();

        public AirshipInputState InputState
        {
            get => _netInputState.Value;
            set
            {
                if (IsOwner) _netInputState.Value = value;
            }
        }

        public ulong? OwnerId => _netOwnerId.Value == ulong.MaxValue ? null : _netOwnerId.Value;
        public ulong? PossessorId => _netPossessorId.Value == ulong.MaxValue ? null : _netPossessorId.Value;

        // IMovingGround Implementation
        public Vector3 Velocity => _view.Velocity;
        public Vector3 PositionDelta => _view.PositionDelta;
        public Quaternion RotationDelta => _view.RotationDelta;

        public void ApplyMovement(Vector3 velocity, Vector3 angularVelocity) => _view.ApplyMovement(velocity, angularVelocity);
        public bool IsCapturedBy(ulong playerId) => PossessorId == playerId;

        public override void OnNetworkSpawn()
        {
            _view = GetComponent<AirshipControllerView>();
            base.OnNetworkSpawn();

            if (IsServer)
            {
                _netOwnerId.Value = OwnerClientId;
            }
        }

        public void OnPossessed(ulong playerId)
        {
            if (IsServer)
            {
                _netPossessorId.Value = playerId;
                GetComponent<NetworkObject>().ChangeOwnership(playerId);
            }
            _view.OnPossessed(playerId);
        }

        public void OnUnpossessed()
        {
            if (IsServer)
            {
                _netPossessorId.Value = ulong.MaxValue;
                GetComponent<NetworkObject>().RemoveOwnership();
            }
            _view.OnUnpossessed();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                _netInputState.Value = InputState;
                _netPosition.Value = transform.position;
                _netRotation.Value = transform.rotation;
            }
            else
            {
                // Drift correction for non-owners simulating the ship locally based on InputState
                if (Vector3.Distance(transform.position, _netPosition.Value) > 0.5f)
                {
                    transform.position = Vector3.Lerp(transform.position, _netPosition.Value, Time.deltaTime * 5f);
                }

                if (Quaternion.Angle(transform.rotation, _netRotation.Value) > 5f)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, _netRotation.Value, Time.deltaTime * 5f);
                }
            }
        }
    }
}
