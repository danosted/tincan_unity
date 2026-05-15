using Unity.Netcode;
using UnityEngine;
using TinCan.Features.Airship;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using TinCan.Features.Interaction;
using TinCan.Features.Events;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Abilities;
using TinCan.Network.Infrastructure.Abilities;
using System;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Bridges the Airship logic with Netcode for GameObjects.
    /// Handles synchronization of steering input and possession state.
    /// </summary>
    [RequireComponent(typeof(AirshipControllerView))]
    [RequireComponent(typeof(NetworkTransformMediator))]
    [RequireComponent(typeof(AbilityNetworkMediator))]
    public class AirshipNetworkMediator : NetworkMediator, IAirshipView, TinCan.Features.FreeCamera.IHasOrbitalCamera, IShipState
    {
        private AirshipControllerView _view;
        private AbilityNetworkMediator _abilitySync;
        private AirshipAttributeSet _attributes;

        [Header("GAS Attributes")]
        [SerializeField] private GameplayAttribute _flightSpeedAttribute;
        [SerializeField] private GameplayAttribute _turnSpeedAttribute;
        [SerializeField] private GameplayAttribute _healthAttribute;

        // IShipState Implementation
        public IAbilityControllerBase Controller => _abilitySync;

        private readonly NetworkVariable<AirshipInputState> _netInputState = new NetworkVariable<AirshipInputState>(
            writePerm: NetworkVariableWritePermission.Owner);

        // IHasOrbitalCamera Implementation
        public TinCan.Features.HumanoidMovement.IOrbitalLookView Look => _view.Look;

        // IAirshipView Implementation (Forwarding to view or using attributes)
        public Transform Transform => _view.transform;
        public float MaxForwardSpeed => _attributes?.MoveSpeed ?? _view.MaxForwardSpeed;
        public float MaxBackwardSpeed => _view.MaxBackwardSpeed;
        public float AccelerationRate => _view.AccelerationRate;
        public float DecelerationRate => _view.DecelerationRate;
        public float AngularAcceleration => _view.AngularAcceleration;
        public float AngularDeceleration => _view.AngularDeceleration;
        public float VelocityBlendRate => _view.VelocityBlendRate;
        public float TurnSpeed => _attributes?.TurnSpeed ?? _view.TurnSpeed;
        public float PitchSpeed => _view.PitchSpeed;
        public float MaxBankAngle => _view.MaxBankAngle;
        public float BankSpeed => _view.BankSpeed;

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

        public bool IsControlsEnabled => _view.IsControlsEnabled;

        public void ApplyMovement(Vector3 velocity, Vector3 angularVelocity) => _view.ApplyMovement(velocity, angularVelocity);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _view = GetComponent<AirshipControllerView>();
            _abilitySync = GetComponent<AbilityNetworkMediator>();

            // Initialize and register attributes
            _attributes = new AirshipAttributeSet(_abilitySync, _flightSpeedAttribute, _turnSpeedAttribute, _healthAttribute);
            _attributes.InitializeBaseValues(_view.MaxForwardSpeed, _view.TurnSpeed, 1000f);
            _abilitySync.RegisterAttributeSet(_attributes);

            if (Registry != null)
            {
                Registry.Register(this);
            }
        }

        public void EnableControls()
        {
            _view.EnableControls();
        }

        public void DisableControls()
        {
            _view.DisableControls();
        }
    }
}
