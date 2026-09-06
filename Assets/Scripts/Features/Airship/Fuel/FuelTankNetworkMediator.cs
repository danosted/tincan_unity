#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Attributes;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Infrastructure Layer: the ship's fuel tank. Stores the level as the Attr_Fuel gameplay attribute on the
    /// airship's ability controller (replicated by AbilityNetworkMediator), so clients read it for free and only
    /// the server ever writes it. Root of the FuelSystem fixture, which is its own NetworkObject spawned and parented
    /// to the airship at runtime; the tank binds to the ship when it is attached (server) or parented (clients).
    /// </summary>
    public class FuelTankNetworkMediator : NetworkBehaviour, IFuelTank, IShipModule
    {
        [SerializeField] private GameplayAttribute? _fuelAttribute;
        [SerializeField] private FuelConfig? _config;

        private FuelAttributeSet? _attributes;
        private IAbilityControllerBase? _controller;
        private IActorOrchestrator _orchestrator = null!;
        private readonly FuelConsumptionProcessor _processor = new();

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => IsSpawned;
        public string ModuleName => "FuelSystem";

        public FuelConfig? Config => _config;
        public float Capacity => _config != null ? _config.Capacity : 0f;
        public float Level => _attributes?.Level ?? 0f;
        public bool IsEmpty => Level <= 0f;

        [Inject]
        public void Construct(IActorOrchestrator orchestrator) => _orchestrator = orchestrator;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _orchestrator.RegisterHierarchy(gameObject);
            TryBindToParentShip();
        }

        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            base.OnNetworkObjectParentChanged(parentNetworkObject);
            TryBindToParentShip();
        }

        public override void OnNetworkDespawn()
        {
            OnDetachedFromShip();
            _orchestrator.UnregisterHierarchy(gameObject);
            base.OnNetworkDespawn();
        }

        public void OnAttachedToShip(IActor ship)
        {
            if (ship is not Component shipComponent) return;
            Bind(shipComponent.GetComponent<IAbilityControllerBase>(), shipComponent.GetComponent<IShipModuleRegistry>());
        }

        public void OnDetachedFromShip()
        {
            _orchestrator.UnregisterShipModule(this);
            _controller = null;
            _attributes = null;
        }

        public void Consume(float amount)
        {
            if (!IsServer || _attributes == null || amount <= 0f) return;
            _attributes.SetLevel(_processor.ClampLevel(Level - amount, Capacity));
        }

        public float Refill(float amount)
        {
            if (!IsServer || _attributes == null || amount <= 0f) return 0f;

            float before = Level;
            float after = _processor.ClampLevel(before + amount, Capacity);
            if (Mathf.Approximately(before, after)) return 0f;

            _attributes.SetLevel(after);
            return after - before;
        }

        private void TryBindToParentShip()
        {
            var parent = transform.parent;
            if (parent == null)
            {
                OnDetachedFromShip();
                return;
            }
            Bind(parent.GetComponentInParent<IAbilityControllerBase>(), parent.GetComponentInParent<IShipModuleRegistry>());
        }

        private void Bind(IAbilityControllerBase? controller, IShipModuleRegistry? registry)
        {
            if (registry != null) _orchestrator.RegisterShipModule(this, registry);
            else _orchestrator.UnregisterShipModule(this);

            if (ReferenceEquals(_controller, controller) && _attributes != null) return;
            _controller = controller;
            _attributes = null;
            if (controller == null || _fuelAttribute == null || _config == null)
            {
                Debug.LogWarning($"[{nameof(FuelTankNetworkMediator)}] Cannot bind: missing ship ability controller, fuel attribute or config.", this);
                return;
            }

            _attributes = new FuelAttributeSet(controller, _fuelAttribute);
            if (IsServer && !_attributes.HasValue)
            {
                _attributes.SetLevel(_processor.ClampLevel(_config.InitialLevel, _config.Capacity));
            }
        }
    }
}
