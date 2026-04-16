namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Domain Layer: Interface for a networked actor (usually a vehicle) that can request to be exited.
    /// </summary>
    public interface IExitVehicleMediator
    {
        void RequestExitVehicle();
    }
}
