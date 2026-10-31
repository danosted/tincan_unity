#nullable enable
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>
    /// A damage point <see cref="ShipDamagePointSpawnUseCase"/> spawns on a designed ship: it carries the index and the
    /// fitting's socket it is given (replicated), instead of reading them from its place in a fixture.
    /// </summary>
    public interface IDesignedDamagePoint
    {
        /// <summary>Server: the point's index on the ship, and the socket of the fitting it sits beside (null: a hull spot).</summary>
        void ServerAssign(int index, ShipSocketId? socket);
    }
}
