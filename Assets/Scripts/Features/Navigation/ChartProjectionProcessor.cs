#nullable enable
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Domain Layer: the chart's projection. The chart is heading-up and centred on the ship: a chart point is a level
    /// offset from the ship turned into the ship's heading and divided by the range, so the chart's edge (radius 1) is
    /// <c>range</c> metres away and +y on the chart is straight over the bow. Altitude is kept apart: the chart is drawn in
    /// 3D, with marks raised by their height above the ship.
    /// </summary>
    public class ChartProjectionProcessor
    {
        /// <summary>Where <paramref name="world"/> lies on the chart (radius 1 is the edge; outside it is off the chart).</summary>
        public Vector2 ToChart(Vector3 world, Vector3 shipPosition, float shipYawDegrees, float range)
        {
            Vector3 level = Quaternion.Euler(0f, -shipYawDegrees, 0f) * (world - shipPosition);
            return new Vector2(level.x, level.z) / Mathf.Max(range, 1e-3f);
        }

        /// <summary>The world point at chart point <paramref name="chart"/>, at <paramref name="altitude"/>.</summary>
        public Vector3 ToWorld(Vector2 chart, float altitude, Vector3 shipPosition, float shipYawDegrees, float range)
        {
            Vector3 level = Quaternion.Euler(0f, shipYawDegrees, 0f) * new Vector3(chart.x, 0f, chart.y) * range;
            return new Vector3(shipPosition.x + level.x, altitude, shipPosition.z + level.z);
        }

        /// <summary>A point beyond the edge, pulled onto it (marks for faraway points of interest sit at the rim).</summary>
        public Vector2 ClampToChart(Vector2 chart) => chart.sqrMagnitude > 1f ? chart.normalized : chart;

        /// <summary>The ship's heading in degrees (its forward, levelled); the chart turns with it.</summary>
        public float Heading(Quaternion shipRotation)
        {
            Vector3 forward = Vector3.ProjectOnPlane(shipRotation * Vector3.forward, Vector3.up);
            return forward.sqrMagnitude < 1e-6f ? shipRotation.eulerAngles.y : Quaternion.LookRotation(forward).eulerAngles.y;
        }
    }
}
