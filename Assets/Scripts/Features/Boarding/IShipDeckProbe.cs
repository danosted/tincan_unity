#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Boarding
{
    /// <summary>Whether a ship has something to stand on under a point: a built ship's deck may arrive a tick or more
    /// after the ship itself (a ship built from a design).</summary>
    public interface IShipDeckProbe
    {
        bool HasDeckUnder(IAirshipView ship, Vector3 position);
    }
}
