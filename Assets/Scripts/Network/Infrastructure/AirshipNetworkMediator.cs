using Unity.Netcode;
using UnityEngine;
using TinCan.Core.Ship;
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Core.Gas;
using TinCan.Network.Infrastructure.Abilities;
using System;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Bridges the Airship logic with Netcode for GameObjects.
    /// The ship is simulated on the server only; its steering comes from its pilot (IAirshipPilotInput, the helm).
    /// </summary>
    [RequireComponent(typeof(AirshipControllerView))]
    [RequireComponent(typeof(NetworkTransformMediator))]
    [RequireComponent(typeof(AbilityNetworkMediator))]
    public class AirshipNetworkMediator : NetworkMediator, IAirshipView, IShipState
    {
        public override bool IsSimulating => IsSpawned && IsServer;

        private AirshipControllerView _view;
        private AbilityNetworkMediator _abilitySync;
        private AirshipAttributeSet _attributes;
        private HealthAttributeSet _health;

        [Header("GAS Attributes")]
        [SerializeField] private GameplayAttribute _flightSpeedAttribute;
        [SerializeField] private GameplayAttribute _turnSpeedAttribute;
        [SerializeField] private System.Collections.Generic.List<AbilityDefinition> _startingAbilities;
        [SerializeField] private HealthAttribute _healthAttribute;
        [SerializeField] private MaxHealthAttribute _maxHealthAttribute;
        [SerializeField] private float _maxHealth = 1000f;

        // IShipState Implementation
        public IAbilityControllerBase Controller => _abilitySync;

        // IAirshipView Implementation (Forwarding to view or using attributes)
        public Transform Transform => _view.transform;
        public float MaxForwardSpeed => _attributes?.MoveSpeed ?? _view.MaxForwardSpeed;
        // A zero flight-speed override disables propulsion in both directions (for example, an empty fuel tank).
        public float MaxBackwardSpeed => MaxForwardSpeed > 0f ? _view.MaxBackwardSpeed : 0f;
        public float AccelerationRate => _view.AccelerationRate;
        public float DecelerationRate => _view.DecelerationRate;
        public float AngularAcceleration => _view.AngularAcceleration;
        public float AngularDeceleration => _view.AngularDeceleration;
        public float VelocityBlendRate => _view.VelocityBlendRate;
        public float TurnSpeed => _attributes?.TurnSpeed ?? _view.TurnSpeed;
        public float PitchSpeed => _view.PitchSpeed;
        public float MaxBankAngle => _view.MaxBankAngle;
        public float BankSpeed => _view.BankSpeed;

        /// <summary>Server: the pilot's input this tick, set by AirshipMovementUseCase (fuel reads the throttle). Not replicated.</summary>
        public AirshipInputState InputState { get; set; }

        // IMovingGround Implementation
        public Vector3 Velocity => _view.Velocity;
        public Vector3 PositionDelta => _view.PositionDelta;
        public Quaternion RotationDelta => _view.RotationDelta;
        public Vector3 GetPointVelocity(Vector3 worldPoint) => _view.GetPointVelocity(worldPoint);

        public void ApplyMovement(Vector3 velocity, Vector3 angularVelocity) => _view.ApplyMovement(velocity, angularVelocity);
        public void Simulate(float deltaTime) => _view.Simulate(deltaTime);

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _view = GetComponent<AirshipControllerView>();
            _abilitySync = GetComponent<AbilityNetworkMediator>();

            // Initialize and register attributes
            _attributes = new AirshipAttributeSet(_abilitySync, _flightSpeedAttribute, _turnSpeedAttribute);
            _attributes.InitializeBaseValues(_view.MaxForwardSpeed, _view.TurnSpeed);
            _abilitySync.RegisterAttributeSet(_attributes);

            _health = new HealthAttributeSet(_abilitySync, _healthAttribute, _maxHealthAttribute);
            _health.InitializeBaseValues(_maxHealth);
            _abilitySync.RegisterAttributeSet(_health);
            
            // TODO: temporary bridge until abilities are granted via equipment/skill tree instead of starting lists.
            foreach (var ability in _startingAbilities ?? new System.Collections.Generic.List<AbilityDefinition>())
            {
                _abilitySync.GrantAbility(ability);
            }
        }
    }
}
