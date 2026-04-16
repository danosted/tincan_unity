using Unity.Netcode;
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

        public void HandleInteraction(ulong interactorId, NetworkObject target)
        {
            if (target == null) return;

            // Check for Vehicle Boarding
            var boardable = target.GetComponentInChildren<IVehicleBoardable>();
            if (boardable != null)
            {
                Debug.Log($"[InteractionOrchestrator] Routing vehicle boarding interaction for player {interactorId}");
                _vehicleBoardingUseCase.BoardVehicle(interactorId, boardable);
                return;
            }

            // Future interactions (Doors, Items, etc.) would be added here
            Debug.LogWarning($"[InteractionOrchestrator] No handler found for interaction with {target.name}");
        }

        public void HandleExit(ulong interactorId)
        {
            Debug.Log($"[InteractionOrchestrator] Routing exit request for player {interactorId}");
            _vehicleBoardingUseCase.ExitVehicle(interactorId);
        }
    }
}
