#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship.Sockets
{
    /// <summary>
    /// A mount point on a ship part's prefab: where a fitting (a cannon station, a tool rack) can be attached. Its
    /// transform is the fitting's pose; up is the way the fitting stands. The builder places parts and never reads these;
    /// the socket integration does (<see cref="IShipSockets"/>). Plan: .docs/plans/modular-airship-builder.md (S5).
    /// </summary>
    public sealed class ShipSocket : MonoBehaviour
    {
    }
}
