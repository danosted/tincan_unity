#nullable enable
using TinCan.Features.HumanoidMovement;
using TinCan.Features.Interaction;

namespace TinCan.Features.Stations
{
    /// <summary>Server-side handler for stations (IA_Occupy* assets pointing here): the interacting player takes the station.</summary>
    public class OccupyStationInteractionHandler : IInteractionHandler
    {
        private readonly IStationOccupancy _occupancy;

        public OccupyStationInteractionHandler(IStationOccupancy occupancy)
        {
            _occupancy = occupancy;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is IStation station && context.Requester is IHumanoidCharacterView player)
            {
                _occupancy.TryOccupy(player, station);
            }
        }
    }
}
