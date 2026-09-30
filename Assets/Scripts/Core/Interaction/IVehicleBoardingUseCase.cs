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
        /// <summary>Lets go of the vehicle this player controls; false when they are in their own body.</summary>
        bool ExitVehicle();
    }
}
