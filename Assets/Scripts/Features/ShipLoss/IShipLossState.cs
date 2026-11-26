#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.ShipLoss
{
    /// <summary>
    /// Whether the ship is lost, as every peer sees it: replicated by the ShipLossState fixture on the ship, so hosts,
    /// clients and late joiners agree. The server writes it (<see cref="ServerSetLost"/> does nothing elsewhere); any
    /// peer may ask for a respawn.
    /// </summary>
    public interface IShipLossState : IActor
    {
        bool IsLost { get; }

        /// <summary>Any peer: ask the server to bring the lost ship back at its last safe spot.</summary>
        void RequestRespawn();

        /// <summary>Server: true once per respawn request since the last call.</summary>
        bool ConsumeRespawnRequest();

        void ServerSetLost(bool lost);
    }
}
