using System;
using TinCan.Core.Domain;
using TinCan.Features.Airship;
using TinCan.Features.Possession;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Application Layer: Handles the logic of boarding and exiting vehicles.
    /// Manages the "parked" state of humanoid characters while their player is controlling a vehicle.
    /// </summary>
    public class VehicleBoardingUseCase : IVehicleBoardingUseCase, ITickable
    {
        private readonly PossessionUseCase _possessionUseCase;
        private readonly IPossessionAuthority _possessionAuthority;
        private readonly IInputService _inputService;

        public VehicleBoardingUseCase(
            PossessionUseCase possessionUseCase,
            IPossessionAuthority possessionAuthority,
            IInputService inputService)
        {
            _possessionUseCase = possessionUseCase;
            _possessionAuthority = possessionAuthority;
            _inputService = inputService;
        }

        public void BoardVehicle(Guid requesterActorId, IVehicleBoardable boardable)
        {
            if (_possessionAuthority.TryAcquirePossession(requesterActorId, boardable.TargetVehicle))
            {
                Debug.Log($"[VehicleBoardingUseCase] Actor {requesterActorId} boarded vehicle.");
            }
        }

        public void ExitVehicle()
        {
            // Return to body (identity handled by API)
            if (_possessionUseCase.CurrentPossession == null || _possessionUseCase.CurrentPossession == _possessionUseCase.PlayerActor)
            {
                return;
            }

            _possessionUseCase.ReleaseCurrentPossession();
            Debug.Log($"[VehicleBoardingUseCase] Requested vehicle exit.");
        }

        public void Tick()
        {
            if (_inputService.WasActionTriggered(ActionNames.Cancel))
            {
                // 1. Check if we are currently in a vehicle and want to exit
                ExitVehicle();
            }
        }
    }
}
