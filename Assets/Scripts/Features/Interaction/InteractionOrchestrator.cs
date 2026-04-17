using TinCan.Core.Domain;
using UnityEngine;
using System.Collections.Generic;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Application Layer: Orchestrates interaction requests by routing them to specific handlers.
    /// This is where the "handling" logic is decoupled from the Views.
    /// </summary>
    public class InteractionOrchestrator : IInteractionOrchestrator
    {
        private readonly IVehicleBoardingUseCase _vehicleBoardingUseCase;

        public InteractionOrchestrator(IVehicleBoardingUseCase vehicleBoardingUseCase)
        {
            _vehicleBoardingUseCase = vehicleBoardingUseCase;
        }

        public void HandleInteraction(IInteractable target)
        {
            if (target == null) return;

            // Check for specific specialized interaction logic (e.g. Boarding)
            if (target is not MonoBehaviour targetMono) return;

            var boardable = targetMono.GetComponentInChildren<IVehicleBoardable>();
            if (boardable != null)
            {
                Debug.Log($"[InteractionOrchestrator] Routing vehicle boarding interaction");
                _vehicleBoardingUseCase.BoardVehicle(boardable);
                return;
            }


            // Future interactions (Doors, Items, etc.) would be added here as specialized Use Cases
            Debug.LogWarning($"[InteractionOrchestrator] No specialized handler found for interaction with {target.GetType().Name}");
        }

        public void HandleExit()
        {
            Debug.Log($"[InteractionOrchestrator] Routing exit request");
            _vehicleBoardingUseCase.ExitVehicle();
        }
    }
}
