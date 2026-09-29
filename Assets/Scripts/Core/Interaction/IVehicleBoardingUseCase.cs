using TinCan.Core.Interaction;

using System;

namespace TinCan.Core.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for the vehicle boarding use case.
    /// Handles the logic of transferring possession between a character and a vehicle.
    /// </summary>
    public interface IVehicleBoardingUseCase
    {
        void BoardVehicle(Guid requesterActorId, IVehicleBoardable boardable);
        void ExitVehicle();
    }
}
