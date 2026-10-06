#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// What is mounted in a ship's sockets, replicated to every peer by the ShipSocketsState fixture. The server writes it;
    /// a player's peer opens the fitting menu when the server asks and sends the choice back as a request.
    /// </summary>
    public interface IShipFittingState : IActor
    {
        IAirshipView? Ship { get; }

        IReadOnlyList<MountedFitting> Mounted { get; }

        void ServerSetMounted(IReadOnlyList<MountedFitting> mounted);

        /// <summary>Server: asks one player's peer to show the fitting menu for a socket.</summary>
        void ServerOpenChooser(ulong clientId, ShipSocketId socket);

        /// <summary>This peer: the socket the server asked this player to choose a fitting for, once.</summary>
        bool TryTakeChooser(out ShipSocketId socket);

        /// <summary>This peer: asks the server to mount a fitting.</summary>
        void RequestMount(ShipSocketId socket, string fittingId);

        /// <summary>Server: the requests received since the last call.</summary>
        IReadOnlyList<MountRequest> TakeRequests();
    }
}
