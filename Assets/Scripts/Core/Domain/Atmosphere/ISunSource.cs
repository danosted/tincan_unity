#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Atmosphere
{
    /// <summary>
    /// Where the sun is now, from the feature that runs the day (DayCycle). The atmosphere (<see cref="IAtmosphereOwner"/>)
    /// points the scene's sun along it and lights the look by its height; without one the look's own fixed sun stays.
    /// Presentation, computed on every peer from synced time, never replicated. Plan: .docs/plans/day-cycle.md (D2).
    /// </summary>
    public interface ISunSource
    {
        /// <summary>The hour of the day, 0..24.</summary>
        float Hour { get; }

        /// <summary>Degrees above the horizon; negative below it.</summary>
        float Elevation { get; }

        /// <summary>Unit world direction toward the sun.</summary>
        Vector3 ToSun { get; }
    }
}
