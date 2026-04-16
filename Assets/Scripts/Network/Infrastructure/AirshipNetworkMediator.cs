using Unity.Netcode;
using UnityEngine;
using TinCan.Features.Airship;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using TinCan.Features.Interaction;
using System;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Bridges the Airship logic with Netcode for GameObjects.
    /// Handles synchronization of steering input and possession state.
    /// </summary>
    [RequireComponent(typeof(AirshipControllerView))]
    [RequireComponent(typeof(NetworkTransformMediator))]
    public class AirshipNetworkMediator : NetworkMediator, IAirshipView, IExitVehicleMediator
    {
        private AirshipControllerView _view;

        private readonly NetworkVariable<AirshipInputState> _netInputState = new NetworkVariable<AirshipInputState>(
            writePerm: NetworkVariableWritePermission.Owner);

        // IAirshipView Implementation (Forwarding to view)
        public Transform Transform => _view.transform;
        public float MaxForwardSpeed => _view.MaxForwardSpeed;
        public float MaxBackwardSpeed => _view.MaxBackwardSpeed;
        public float AccelerationRate => _view.AccelerationRate;
        public float DecelerationRate => _view.DecelerationRate;
        public float TurnSpeed => _view.TurnSpeed;
        public float PitchSpeed => _view.PitchSpeed;

        public AirshipInputState InputState
        {
            get => _netInputState.Value;
            set
            {
                if (IsOwner) _netInputState.Value = value;
            }
        }

        // IMovingGround Implementation
        public Vector3 Velocity => _view.Velocity;
        public Vector3 PositionDelta => _view.PositionDelta;
        public Quaternion RotationDelta => _view.RotationDelta;

        public void ApplyMovement(Vector3 velocity, Vector3 angularVelocity) => _view.ApplyMovement(velocity, angularVelocity);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _view = GetComponent<AirshipControllerView>();
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                _netInputState.Value = InputState;
            }
        }

        public void RequestExitVehicle()
        {
            if (IsOwner)
            {
                RequestExitVehicleServerRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestExitVehicleServerRpc()
        {
            InteractionOrchestrator?.HandleExit(OwnerClientId);
        }
    }
}
