#nullable enable
using TinCan.Core.Ship;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>The ship's solid body, as hazards see it. A seam over physics, so the use case stays testable.</summary>
    public interface IShipContactQuery
    {
        /// <summary>Does a sphere at this point touch the ship?</summary>
        bool Touches(IAirshipView ship, Vector3 point, float radius);
    }
}
