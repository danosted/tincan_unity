#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.Gates
{
    /// <summary>The ship's solid body, as a gate sees it. A seam over physics, so the use case stays testable.</summary>
    public interface IShipOverlapQuery
    {
        /// <summary>Does a sphere at this point touch the ship's solid colliders?</summary>
        bool Overlaps(IAirshipView ship, Vector3 point, float radius);
    }
}
