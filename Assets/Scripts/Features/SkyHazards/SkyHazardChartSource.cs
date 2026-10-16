#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Navigation;
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>
    /// Every peer: the voyage's hazard zones on the navigator's chart, as hazard obstacles spanning their altitude band,
    /// so a route can go around, over or under them. Built from the session's seed like the zones themselves
    /// (<see cref="HazardZoneMap"/>); nothing is replicated. Plan: .docs/plans/navigation-compass.md (S4).
    /// </summary>
    public class SkyHazardChartSource : IChartSource
    {
        private readonly HazardZoneMap _zones;

        public SkyHazardChartSource(HazardZoneMap zones) => _zones = zones;

        public void CollectPointsOfInterest(ICollection<PointOfInterest> into) { }

        public void CollectObstacles(Vector3 center, float radius, ICollection<ChartObstacle> into)
        {
            foreach (var zone in _zones.Zones)
            {
                float dx = zone.Center.x - center.x, dz = zone.Center.z - center.z;
                float limit = radius + zone.Radius;
                if (dx * dx + dz * dz > limit * limit) continue;
                into.Add(new ChartObstacle(zone.Center, zone.Radius, zone.Bottom, zone.Top, ChartObstacleKind.Hazard));
            }
        }
    }
}
