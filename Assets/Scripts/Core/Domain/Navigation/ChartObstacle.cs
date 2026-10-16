#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Navigation
{
    /// <summary>
    /// Something on the chart a route should avoid: a level circle (centre and radius) and the band of altitude it
    /// blocks, so a navigator can route around it, over it or under it.
    /// </summary>
    public readonly struct ChartObstacle
    {
        public readonly Vector3 Center;
        public readonly float Radius;
        public readonly float Bottom;
        public readonly float Top;
        public readonly ChartObstacleKind Kind;

        public ChartObstacle(Vector3 center, float radius, float bottom, float top, ChartObstacleKind kind)
        {
            Center = center;
            Radius = radius;
            Bottom = bottom;
            Top = top;
            Kind = kind;
        }
    }
}
