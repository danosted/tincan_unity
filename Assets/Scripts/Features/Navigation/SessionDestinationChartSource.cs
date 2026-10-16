#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Navigation;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Every peer: the play session's destination (the voyage's, through the replicated <see cref="ISessionLayout"/>) as a
    /// point of interest, known from the start. A new route is laid to it when a ship has none. Absent until a session
    /// begins.
    /// </summary>
    public class SessionDestinationChartSource : IChartSource
    {
        public const int PoiId = 1;

        private readonly IActorRegistry _actors;

        public SessionDestinationChartSource(IActorRegistry actors) => _actors = actors;

        public void CollectPointsOfInterest(ICollection<PointOfInterest> into)
        {
            var layout = _actors.GetActors<ISessionLayout>().FirstOrDefault();
            if (layout == null || layout.LayoutSeed == 0) return;
            into.Add(new PointOfInterest(PoiId, "Destination", layout.Destination, startsKnown: true));
        }

        public void CollectObstacles(Vector3 center, float radius, ICollection<ChartObstacle> into) { }
    }
}
