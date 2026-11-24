#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Server: put a ship somewhere at once (a respawn), without the peers sliding it there. The networked ship implements
    /// it with a network teleport, so clients snap instead of interpolating across the jump. Its speed is not touched:
    /// hold the ship (<see cref="IAirshipHold"/>) to bring it to rest. Plan: .docs/plans/ship-lost-and-respawn.md.
    /// </summary>
    public interface IAirshipTeleport
    {
        void Teleport(Vector3 position, Quaternion rotation);
    }
}
