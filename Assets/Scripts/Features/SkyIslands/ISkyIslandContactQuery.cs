#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>Is the ship inside island rock, and which way out? A seam over physics, so the impact use case stays testable.</summary>
    public interface ISkyIslandContactQuery
    {
        /// <summary>
        /// True when any of the ship's solid colliders is inside an island: <paramref name="push"/> then moves the ship
        /// out of the deepest overlap, and <paramref name="island"/> is that island.
        /// </summary>
        bool TryPushOut(IAirshipView ship, out Vector3 push, out SkyIslandId island);
    }
}
