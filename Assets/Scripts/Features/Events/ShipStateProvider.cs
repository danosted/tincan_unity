using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using System.Linq;
using System;

namespace TinCan.Features.Events
{
    /// <summary>
    /// Application Layer: A provider that resolves the ship's state from the Actor Registry.
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
