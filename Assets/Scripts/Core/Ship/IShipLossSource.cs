#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Server: a feature that can lose a ship, besides its health running out. The cloud boundary is one: a ship that stays
    /// too deep in the cloud for too long is lost with its whole crew. The session (the voyage) reads every source, so
    /// a loss of any kind ends in the same "the ship is lost" screen, and it asks the source for the last place the ship
    /// was safe to respawn it there. Register with <c>.As&lt;IShipLossSource&gt;()</c>. A source that also implements
    /// <see cref="IShipRecovery"/> is told when the ship is brought back, and starts counting afresh.
    /// Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public interface IShipLossSource
    {
        bool IsLost(IAirshipView ship);

        /// <summary>The last pose where this source saw the ship safe; false when it has none.</summary>
        bool TryGetSafePose(IAirshipView ship, out Vector3 position, out Quaternion rotation);
    }
}
