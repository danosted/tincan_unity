#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Core.Domain.Navigation
{
    /// <summary>
    /// Every peer: a feature that puts things on the navigator's chart (sky islands, hazard zones). Navigation asks every
    /// registered source and references none of them; without Navigation nothing asks. Register an implementation with
    /// <c>.As&lt;IChartSource&gt;()</c>. What a source adds must be the same on every peer: build it from replicated or
    /// seeded state. Plan: .docs/plans/navigation-compass.md.
    /// </summary>
    public interface IChartSource
    {
        /// <summary>Adds this source's points of interest for the current session.</summary>
        void CollectPointsOfInterest(ICollection<PointOfInterest> into);

        /// <summary>Adds this source's obstacles within <paramref name="radius"/> metres (level) of <paramref name="center"/>.</summary>
        void CollectObstacles(Vector3 center, float radius, ICollection<ChartObstacle> into);
    }
}
