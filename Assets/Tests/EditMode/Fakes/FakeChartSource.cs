#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Navigation;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A chart source with fixed points of interest and obstacles; obstacles are filtered by range like a real source.</summary>
    public sealed class FakeChartSource : IChartSource
    {
        public List<PointOfInterest> Points { get; } = new();
        public List<ChartObstacle> Obstacles { get; } = new();

        public void CollectPointsOfInterest(ICollection<PointOfInterest> into)
        {
            foreach (var point in Points) into.Add(point);
        }

        public void CollectObstacles(Vector3 center, float radius, ICollection<ChartObstacle> into)
        {
            foreach (var obstacle in Obstacles)
            {
                Vector3 offset = obstacle.Center - center;
                offset.y = 0f;
                if (offset.magnitude - obstacle.Radius <= radius) into.Add(obstacle);
            }
        }
    }
}
