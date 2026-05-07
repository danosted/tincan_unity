using TinCan.Core.Domain;
using UnityEngine;
using System.Collections.Generic;
using TinCan.Features.Events;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Application Layer: Orchestrates interaction requests by routing them to specific handlers.
    /// This is where the "handling" logic is decoupled from the Views.
    /// </summary>
    public class InteractionOrchestrator : IInteractionOrchestrator
    {
        private readonly IVehicleBoardingUseCase _vehicleBoardingUseCase;
        private readonly EventStationUseCase _eventStationUseCase;

        public InteractionOrchestrator(
            IVehicleBoardingUseCase vehicleBoardingUseCase,
            EventStationUseCase eventStationUseCase)
        {
            _vehicleBoardingUseCase = vehicleBoardingUseCase;
            _eventStationUseCase = eventStationUseCase;
        }

        public void HandleInteraction(IActor interactor, IInteractable target)
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

            var eventStation = targetMono.GetComponentInChildren<IEventStation>();
            if (eventStation != null)
            {
                Debug.Log($"[InteractionOrchestrator] Routing event station interaction");
                _eventStationUseCase.HandleStationInteraction(interactor, eventStation);
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
