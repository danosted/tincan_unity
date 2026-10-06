#nullable enable
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Events;
using TinCan.Core.Interaction;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Server: E on a free socket asks the player who pressed it to choose a fitting (the menu opens on their peer only).
    /// The choice comes back as a mount request, which <see cref="ShipFittingUseCase"/> validates.
    /// </summary>
    public sealed class MountFittingInteractionHandler : IInteractionHandler
    {
        private const string LogSource = "ShipFittings";

        private readonly IActorRegistry _actors;
        private readonly IShipFittingCatalog _catalog;
        private readonly IEventPublisher _events;

        public MountFittingInteractionHandler(IActorRegistry actors, IShipFittingCatalog catalog, IEventPublisher events)
        {
            _actors = actors;
            _catalog = catalog;
            _events = events;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is not ShipSocketTarget socket) return;
            if (!socket.IsFree)
            {
                _events.LogInfo(LogSource, $"The {socket.Socket} is taken.");
                return;
            }

            if (_catalog.Fittings.Count == 0)
            {
                _events.LogInfo(LogSource, "No feature loaded offers a fitting to mount.");
                return;
            }

            if (context.Requester is not IPossessable { OwnerId: { } clientId }) return;
            var state = _actors.GetActors<IShipFittingState>().FirstOrDefault(s => s.Ship?.Id == socket.ShipId);
            if (state == null)
            {
                _events.LogWarning(LogSource, $"Ship {socket.ShipId} has no ShipSocketsState fixture.");
                return;
            }

            state.ServerOpenChooser(clientId, socket.Socket);
        }
    }
}
