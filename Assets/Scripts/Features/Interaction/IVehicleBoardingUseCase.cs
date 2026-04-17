using TinCan.Features.Interaction;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for the vehicle boarding use case.
    /// Handles the logic of transferring possession between a character and a vehicle.
    /// </summary>
    public interface IVehicleBoardingUseCase
    {
        void BoardVehicle(IVehicleBoardable boardable);
        void ExitVehicle();
    }
}
