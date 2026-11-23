#nullable enable
using TinCan.Core.Interaction;
using TinCan.Core.Items;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Presentation/Infrastructure Layer: the motor's filler cap. It takes a held fuel crystal or jerry can
    /// (<see cref="IItemConsumer"/>, through core's ItemInteractionHandler and IA_PourFuel) for its litres
    /// (<see cref="FuelFeed"/>), while the tank has room. Sits under the FuelSystem fixture or the
    /// fuel.receiver fitting, and finds the tank on the ship it is parented to.
    /// </summary>
    public class MotorFillPortNetworkMediator : NetworkBehaviour, IInteractionTarget, IFuelFillPort, IItemConsumer
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;

        private IFuelTank? _tank;

        public InteractionDefinition Definition => _interactionDefinition!;

        public IFuelTank? Tank
        {
            get
            {
                if (!FuelTankLocator.IsAlive(_tank)) _tank = FuelTankLocator.FindFor(this);
                return _tank;
            }
        }

        public bool Accepts(ItemDefinition item) => Tank?.Config is { } config && FuelFeed.Litres(item, config) > 0f;

        public bool TryConsume(ItemDefinition item) =>
            Tank is { Config: { } config } tank && tank.Refill(FuelFeed.Litres(item, config)) > 0f;
    }
}
