#nullable enable
using TinCan.Core.Interaction;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>
    /// A hand crank by the engine (the socket fitting fuel.crank): with the tank low, each press of Primary on it charges
    /// a little power into the tank (<see cref="CrankTurnUseCase"/>), so a ship that ran dry can limp to the next island.
    /// </summary>
    public interface IFuelCrank : IInteractionTarget
    {
        /// <summary>The tank of the ship this crank is on.</summary>
        IFuelTank? Tank { get; }

        /// <summary>Turns so far, replicated: every peer turns the handle a quarter for each.</summary>
        int Turns { get; }

        /// <summary>Server only: one turn was made.</summary>
        void ServerTurned();
    }
}
