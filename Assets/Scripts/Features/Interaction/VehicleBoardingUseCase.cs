using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Application Layer: Handles the logic of boarding and exiting vehicles.
    /// Manages the "parked" state of humanoid characters while their player is controlling a vehicle.
    /// </summary>
    public class VehicleBoardingUseCase : IVehicleBoardingUseCase
    {
        private readonly PossessionUseCase _possessionUseCase;

        public VehicleBoardingUseCase(PossessionUseCase possessionUseCase)
        {
            _possessionUseCase = possessionUseCase;
        }

        public void BoardVehicle(ulong playerId, IVehicleBoardable boardable)
        {
            // 3. Swap possession to the vehicle
            _possessionUseCase.Possess(boardable.TargetVehicle);

            Debug.Log($"[VehicleBoardingUseCase] Player {playerId} boarded vehicle.");
        }

        public void ExitVehicle(ulong playerId)
        {
            _possessionUseCase.Possess(null);
            Debug.Log($"[VehicleBoardingUseCase] Player {playerId} exited vehicle and returned to character.");
        }
    }
}
