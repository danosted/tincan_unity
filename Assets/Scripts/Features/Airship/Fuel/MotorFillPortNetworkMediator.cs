#nullable enable
using TinCan.Features.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// Presentation/Infrastructure Layer: the motor's filler cap. Interacting with it routes to
    /// PourFuelInteractionHandler via its InteractionDefinition (IA_PourFuel). Sits under the FuelSystem
    /// fixture so it can find the tank on a parent.
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
                if (!FuelTankLocator.IsAlive(_tank)) _tank = GetComponentInParent<IFuelTank>();
                return _tank;
            }
        }
    }
}
