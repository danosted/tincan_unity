#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Navigation
{
    /// <summary>
    /// Domain Layer: editing a route on the chart, in chart points (the ship at the centre). A click near a leg bends that
    /// leg: the new waypoint goes into the nearest leg still ahead (from the ship to the active waypoint, then on), at the
    /// height the leg has there. Removing a waypoint keeps the compass on the same target where it can.
    /// </summary>
    public class RouteEditProcessor
    {
        /// <summary>The index of the point nearest <paramref name="cursor"/> within <paramref name="radius"/>, or -1.</summary>
        public int Nearest(IReadOnlyList<Vector2> points, Vector2 cursor, float radius)
        {
            int best = -1;
            float bestDistance = radius;
            for (int i = 0; i < points.Count; i++)
            {
                float distance = Vector2.Distance(points[i], cursor);
                if (distance > bestDistance) continue;
                best = i;
                bestDistance = distance;
            }
            return best;
        }

        /// <summary>
        /// Where a new waypoint at <paramref name="cursor"/> goes, and how far along its leg it lies (0 at the leg's start,
        /// 1 at its end): into the nearest leg from the active waypoint on, the first leg starting at the ship (the centre).
        /// </summary>
        public (int Index, float Along) Insertion(IReadOnlyList<Vector2> points, int active, Vector2 cursor)
        {
            if (points.Count == 0) return (0, 1f);

            active = Mathf.Clamp(active, 0, points.Count - 1);
            int best = active;
            float bestAlong = 1f;
            float bestDistance = float.MaxValue;
            for (int end = active; end < points.Count; end++)
            {
                Vector2 start = end == active ? Vector2.zero : points[end - 1];
                float along = Along(start, points[end], cursor);
                float distance = Vector2.Distance(Vector2.Lerp(start, points[end], along), cursor);
                if (distance >= bestDistance) continue;
                best = end;
                bestAlong = along;
                bestDistance = distance;
            }
            return (best, bestAlong);
        }

        /// <summary>The active index after removing <paramref name="removed"/>, so the compass keeps its target where it can.</summary>
        public int ActiveAfterRemoval(int active, int removed, int countAfter)
        {
            if (countAfter <= 0) return -1;
            if (removed < active) active--;
            return Mathf.Clamp(active, 0, countAfter - 1);
        }

        private static float Along(Vector2 start, Vector2 end, Vector2 point)
        {
            Vector2 leg = end - start;
            return leg.sqrMagnitude < 1e-8f ? 0f : Mathf.Clamp01(Vector2.Dot(point - start, leg) / leg.sqrMagnitude);
        }
    }
}
