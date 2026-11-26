#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.ShipLoss
{
    /// <summary>
    /// The ship lost with its whole crew, and brought back. Any peer reads <see cref="IsLost"/> (replicated); the server
    /// brings the ship back. A session that runs on top (the voyage) ends on a loss and brings the ship back its own way
    /// (Restart, at the voyage's start). Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public interface IShipLoss
    {
        bool IsLost { get; }

        /// <summary>Server: the ship placed there, made whole (<see cref="IShipRecovery"/>), its crew put back aboard; no
        /// longer lost.</summary>
        void BringBack(IAirshipView ship, Vector3 position, Quaternion rotation);
    }
}
