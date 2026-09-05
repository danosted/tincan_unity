#nullable enable

using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.Abilities;
using TinCan.Features.Interaction;
using UnityEngine;

namespace TinCan.Network.Infrastructure
{
    public class ShipRepairPointNetworkMediator : NetworkMediator, IRepairable, IInteractionTarget
    {
        [SerializeField] private InteractionDefinition _interactionDefinition = null!;

        private HealthAttributeSet? _shipHealth;
        private IAbilityControllerBase? _shipController;

        public IAbilityControllerBase? Controller => _shipController;
        public float HealthPercentage => _shipHealth?.HealthPercentage ?? 0f;
        public bool IsBroken => _shipHealth?.IsBroken ?? true;
        public InteractionDefinition Definition => _interactionDefinition;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            _shipController = GetComponentInParent<IAbilityControllerBase>();
            if (_shipController != null && _shipController.TryGetAttributeSet<HealthAttributeSet>(out var health))
            {
                _shipHealth = health;
            }
        }
    }
}