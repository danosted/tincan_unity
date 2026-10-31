#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Navigation;
using UnityEngine;

namespace TinCan.Features.GasChallenge
{
    /// <summary>
    /// Every peer: the gas field's pockets on the navigator's chart, as gas obstacles spanning their height, until they go
    /// off. Built from the networked pockets (replicated), so crewmates see the same chart; hand-placed scene pockets are
    /// not replicated and stay off it. Read only where Navigation is loaded. Plan: .docs/plans/gas-squall-event.md (G6).
    /// </summary>
    public sealed class GasPocketChartSource : IChartSource
    {
        private readonly IActorRegistry _actors;

        public GasPocketChartSource(IActorRegistry actors) => _actors = actors;

        public void CollectPointsOfInterest(ICollection<PointOfInterest> into) { }

        public void CollectObstacles(Vector3 center, float radius, ICollection<ChartObstacle> into)
        {
            foreach (var pocket in _actors.GetActors<IGasPocket>())
            {
                if (!pocket.IsSimulating || pocket.Detonated) continue;

                Vector3 at = pocket.Center;
                float dx = at.x - center.x, dz = at.z - center.z;
                float limit = radius + pocket.Radius;
                if (dx * dx + dz * dz > limit * limit) continue;

                into.Add(new ChartObstacle(at, pocket.Radius, at.y - pocket.Radius, at.y + pocket.Radius, ChartObstacleKind.Gas));
            }
        }
    }
}
