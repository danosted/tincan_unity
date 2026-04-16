using TinCan.Features.Possession;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for an interactable object that allows a player to board and control a vehicle.
    /// </summary>
    public interface IVehicleBoardable : IInteractable
    {
        /// <summary>
        /// The vehicle entity that will be possessed upon interaction.
        /// </summary>
        IPossessable TargetVehicle { get; }
    }
}
