#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Pure rules of the storm's heart and its cure (plan: storm-crossing.md, ST5). Everything comes from the storm's
    /// seed, the zone's severity and the shared clock, so every peer sees the heart and its target in the same place
    /// without sending them, and the beam hits what the gunner sees:
    /// <list type="bullet">
    /// <item>The heart is at the zone's centre at severity 1, and wanders a figure of eight around it in harder storms.</item>
    /// <item>Time is cut into slots; each slot's first <see cref="TargetSeconds"/> hold one target at a spot round the
    /// heart picked by a hash of the seed and the slot. A target hit once is spent for the rest of its slot.</item>
    /// </list>
    /// </summary>
    public static class StormHeartProcessor
    {
        /// <summary>Where the heart is at <paramref name="time"/>: the zone's centre, plus its wander, at the altitude.</summary>
        public static Vector3 Heart(Vector2 centre, float altitude, int severity, int seed, double time, StormConfig config)
        {
            int steps = Mathf.Clamp(severity, 1, 3) - 1;
            if (steps == 0 || config.HeartWander <= 0f) return new Vector3(centre.x, altitude, centre.y);

            float radius = config.HeartWander * steps;
            double period = config.HeartWanderSeconds / steps;
            float angle = (float)(time / period % 1.0) * 2f * Mathf.PI + StormProcessor.Hash01(seed, 0, 7) * 2f * Mathf.PI;
            // A figure of eight, so it doubles back on itself instead of circling at one speed.
            return new Vector3(centre.x + radius * Mathf.Cos(angle), altitude, centre.y + radius * 0.6f * Mathf.Sin(2f * angle));
        }

        /// <summary>How long a target stays up: shorter in harder storms.</summary>
        public static float TargetSeconds(int severity, StormConfig config) =>
            config.TargetSeconds * (1f - 0.2f * (Mathf.Clamp(severity, 1, 3) - 1));

        /// <summary>How many hits cure the storm: two more for each step of severity.</summary>
        public static int HitsToCure(int severity, StormConfig config) => config.HitsToCure + 2 * (Mathf.Clamp(severity, 1, 3) - 1);

        /// <summary>The slot <paramref name="time"/> falls in.</summary>
        public static long Slot(double time, int severity, StormConfig config) =>
            (long)System.Math.Floor(time / SlotSeconds(severity, config));

        /// <summary>Whether the slot's target is still up at <paramref name="time"/> (it fades after its seconds).</summary>
        public static bool TargetUp(double time, int severity, StormConfig config) => TargetSecondsLeft(time, severity, config) > 0f;

        /// <summary>Seconds the slot's target stays up from <paramref name="time"/> (0 or less: it is gone).</summary>
        public static float TargetSecondsLeft(double time, int severity, StormConfig config)
        {
            double slotSeconds = SlotSeconds(severity, config);
            double into = time - System.Math.Floor(time / slotSeconds) * slotSeconds;
            return (float)(TargetSeconds(severity, config) - into);
        }

        /// <summary>Where the slot's target is, from the heart: a spot on a ring round it, a little above or below.</summary>
        public static Vector3 TargetOffset(long slot, int seed, StormConfig config)
        {
            int lane = (int)(slot % int.MaxValue);
            float angle = StormProcessor.Hash01(seed, lane, 3) * 2f * Mathf.PI;
            float distance = Mathf.Lerp(config.TargetRing.x, config.TargetRing.y, StormProcessor.Hash01(seed, lane, 4));
            float height = (StormProcessor.Hash01(seed, lane, 5) * 2f - 1f) * config.TargetHeight;
            return new Vector3(Mathf.Cos(angle) * distance, height, Mathf.Sin(angle) * distance);
        }

        /// <summary>
        /// The target up at <paramref name="time"/> and its slot, or null: between targets, or the slot's target was
        /// already hit (<paramref name="hitSlot"/>). The caller checks the round runs and the storm still rages.
        /// </summary>
        public static (Vector3 Position, long Slot)? LiveTarget(Vector2 centre, float altitude, int severity, int seed, double time,
            long hitSlot, StormConfig config)
        {
            long slot = Slot(time, severity, config);
            if (slot == hitSlot || !TargetUp(time, severity, config)) return null;
            return (Heart(centre, altitude, severity, seed, time, config) + TargetOffset(slot, seed, config), slot);
        }

        /// <summary>
        /// Metres along a beam (from <paramref name="origin"/>, <paramref name="direction"/> normalized, out to
        /// <paramref name="length"/>) to where it enters a sphere, or null if it misses it.
        /// </summary>
        public static float? Enters(Vector3 origin, Vector3 direction, float length, Vector3 centre, float radius)
        {
            Vector3 toCentre = centre - origin;
            float along = Vector3.Dot(toCentre, direction);
            float squareMiss = toCentre.sqrMagnitude - along * along;
            float squareRadius = radius * radius;
            if (squareMiss > squareRadius) return null;

            float half = Mathf.Sqrt(squareRadius - squareMiss);
            float entry = along - half;
            if (entry < 0f)
            {
                if (along + half < 0f) return null; // wholly behind the muzzle
                entry = 0f; // the beam starts inside it
            }
            return entry <= length ? entry : null;
        }

        private static double SlotSeconds(int severity, StormConfig config) => TargetSeconds(severity, config) + config.TargetGapSeconds;
    }
}
