using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities.Attributes;
using TinCan.Features.Abilities;
using TinCan.Features.Interaction;
using TinCan.Network.Infrastructure.Abilities;
using Unity.Netcode;
using UnityEngine;
using VContainer;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Infrastructure Layer: Base mediator for ship modules.
    /// Handles parenting to the ship and registration with the ship's module registry.
    /// </summary>
    [RequireComponent(typeof(AbilityNetworkMediator))]
    public class ShipModuleNetworkMediator : NetworkMediator, IShipModule, IRepairable, IInteractionTarget
    {
        [SerializeField] private string _moduleName = "Unnamed Module";
        [SerializeField] private HealthAttribute _healthAttribute;
        [SerializeField] private MaxHealthAttribute _maxHealthAttribute;
        [SerializeField] private float _maxHealth = 100f;
        [SerializeField, Range(0f, 1f)] private float _brokenThreshold = 0.1f;
        [SerializeField] private InteractionDefinition _interactionDefinition;

        public string ModuleName => _moduleName;

        private IActor _parentShip;
        private AbilityNetworkMediator _abilitySync;
        private HealthAttributeSet _attributes;

        // IRepairable Implementation
        public TinCan.Core.Domain.Abilities.IAbilityControllerBase Controller => _abilitySync;

        public float HealthPercentage => _attributes?.HealthPercentage ?? 1f;
        public bool IsBroken => _attributes?.IsBroken ?? false;
        public InteractionDefinition Definition => _interactionDefinition;

        public virtual void OnAttachedToShip(IActor ship)
        {
            _parentShip = ship;

            if (ship is MonoBehaviour shipMono)
            {
                var registry = shipMono.GetComponent<IShipModuleRegistry>();
                registry?.RegisterModule(this);
            }
        }

        public virtual void OnDetachedFromShip()
        {
            if (_parentShip is MonoBehaviour shipMono)
            {
                var registry = shipMono.GetComponent<IShipModuleRegistry>();
                registry?.UnregisterModule(this);
            }
            _parentShip = null;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _abilitySync = GetComponent<AbilityNetworkMediator>();

            if (_healthAttribute != null)
            {
                _attributes = new HealthAttributeSet(_abilitySync, _healthAttribute, _maxHealthAttribute, _brokenThreshold);
                _attributes.InitializeBaseValues(_maxHealth);
                _abilitySync.RegisterAttributeSet(_attributes);
            }

            // If we are already parented to a ship on spawn (e.g. late joining)
            if (transform.parent != null)
            {
                var ship = transform.parent.GetComponent<IActor>();
                if (ship != null)
                {
                    OnAttachedToShip(ship);
                }
            }
        }

        public override void OnNetworkDespawn()
        {
            OnDetachedFromShip();
            base.OnNetworkDespawn();
        }
    }
}
