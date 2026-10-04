#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Is this spot inside something solid that a feature put in the world (island rock)? Features that place things in
    /// the sky (hazards) ask every registered query before using a spot; with none registered, every spot is free.
    /// Register an implementation with <c>.As&lt;IWorldObstacleQuery&gt;()</c>. Plan: .docs/plans/sky-islands.md.
    /// </summary>
    public interface IWorldObstacleQuery
    {
        /// <summary>True when a sphere of this radius at this point overlaps an obstacle.</summary>
        bool IsBlocked(Vector3 point, float radius);
    }
}
