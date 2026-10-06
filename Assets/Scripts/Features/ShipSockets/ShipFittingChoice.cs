#nullable enable
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// This peer: the socket the open fitting menu is for. Written when the menu opens, taken by the menu row's command.
    /// Kept apart from the menu so the commands do not depend on the menu system (which collects every command).
    /// </summary>
    public sealed class ShipFittingChoice
    {
        private IShipFittingState? _state;
        private ShipSocketId _socket;

        public void Begin(IShipFittingState state, ShipSocketId socket)
        {
            _state = state;
            _socket = socket;
        }

        public bool TryTake(out IShipFittingState state, out ShipSocketId socket)
        {
            state = _state!;
            socket = _socket;
            if (_state == null) return false;
            _state = null;
            return true;
        }
    }
}
