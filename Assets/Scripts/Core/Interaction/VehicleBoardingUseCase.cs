#nullable enable
using System;
using TinCan.Core.Possession;
using UnityEngine;

namespace TinCan.Core.Interaction
{
    /// <summary>
    /// Application Layer: Handles the logic of boarding and exiting vehicles.
    /// Manages the "parked" state of humanoid characters while their player is controlling a vehicle.
    /// Exiting on Cancel is routed by the Airship input context (<see cref="ExitVehicleInputHandler"/>).
    /// </summary>
    public class VehicleBoardingUseCase : IVehicleBoardingUseCase
    {
        private readonly PossessionUseCase _possessionUseCase;
        private readonly IPossessionAuthority _possessionAuthority;

        public VehicleBoardingUseCase(
            PossessionUseCase possessionUseCase,
            IPossessionAuthority possessionAuthority)
        {
            _possessionUseCase = possessionUseCase;
            _possessionAuthority = possessionAuthority;
        }

        public void BoardVehicle(Guid requesterActorId, IVehicleBoardable boardable)
        {
            if (_possessionAuthority.TryAcquirePossession(requesterActorId, boardable.TargetVehicle))
            {
                Debug.Log($"[VehicleBoardingUseCase] Actor {requesterActorId} boarded vehicle.");
            }
        }

        public bool ExitVehicle()
        {
            // Return to body (identity handled by API)
            if (_possessionUseCase.CurrentPossession == null || _possessionUseCase.CurrentPossession == _possessionUseCase.PlayerActor)
            {
                return false;
            }

            _possessionUseCase.ReleaseCurrentPossession();
            Debug.Log($"[VehicleBoardingUseCase] Requested vehicle exit.");
            return true;
        }
    }
}
