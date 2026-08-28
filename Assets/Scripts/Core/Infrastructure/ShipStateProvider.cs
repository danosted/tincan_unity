using System;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;

namespace TinCan.Core.Infrastructure
{
    /// <summary>
    /// Infrastructure-layer provider that resolves the ship's state from the actor registry.
    /// </summary>
    public class ShipStateProvider : IShipState
    {
        private readonly IActorRegistry _registry;

        public ShipStateProvider(IActorRegistry registry)
        {
            _registry = registry;
        }

        private IShipState GetShip() => _registry.GetActors<IShipState>().FirstOrDefault();

        public Guid Id => GetShip()?.Id ?? Guid.Empty;
        public bool IsSimulating => GetShip()?.IsSimulating ?? false;
        public IAbilityControllerBase Controller => GetShip()?.Controller;
    }
}
