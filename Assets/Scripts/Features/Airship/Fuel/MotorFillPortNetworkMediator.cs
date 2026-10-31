#nullable enable
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Presentation/Infrastructure Layer: the motor's filler cap. Interacting with it routes to
    /// PourFuelInteractionHandler via its InteractionDefinition (IA_PourFuel). Sits under the FuelSystem fixture or the
    /// fuel.receiver fitting, and finds the tank on the ship it is parented to.
    /// </summary>
    public class MotorFillPortNetworkMediator : NetworkBehaviour, IInteractionTarget, IFuelFillPort
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
    }
}
