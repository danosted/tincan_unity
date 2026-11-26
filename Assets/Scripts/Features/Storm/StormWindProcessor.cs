#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// Pure rules of the storm's wind (plan: storm-crossing.md, ST3 gusts and ST4 debris). The wind swirls round the
    /// storm's centre (counter-clockwise seen from above), so "upwind" is always round the disc, never out of it. How hard
    /// it blows is the storm's <see cref="Fury"/>: its weight (depth times strength) leaning toward the eye's edge, where
    /// the storm fights back hardest. Gusts come in waves on the shared clock: time is cut into slots, and whether a slot
    /// holds a gust and how far it swings off the wind come from a hash of the seed and the slot, so every peer feels the
    /// same gust the server pushes the ship with.
    /// </summary>
    public static class StormWindProcessor
    {
        /// <summary>The wind's way at <paramref name="position"/>: a unit horizontal vector round the centre.</summary>
        public static Vector3 Wind(Vector2 centre, Vector3 position)
        {
            var away = new Vector2(position.x - centre.x, position.z - centre.y);
            if (away.sqrMagnitude < 1e-4f) return Vector3.forward;
            away.Normalize();
            return new Vector3(-away.y, 0f, away.x);
        }

        /// <summary>
        /// How hard the storm blows here, 0..1: its weight, leaning toward the eye's edge (0 outside, cured or in the
        /// still eye).
        /// </summary>
        public static float Fury(float depth, float strength) => StormProcessor.Weight(depth, strength) * Mathf.Clamp01(depth);

        /// <summary>The severity's scale on the wind: 1 at severity 1, half again for each step up.</summary>
        public static float SeverityScale(int severity) => 1f + 0.5f * (Mathf.Clamp(severity, 1, 3) - 1);

        /// <summary>
        /// The gust at <paramref name="time"/>: a horizontal world velocity, m/s (zero between gusts). A gust rises and
        /// falls over <see cref="StormConfig.GustSeconds"/> at the start of its slot, blowing along the wind swung by up to
        /// <see cref="StormConfig.GustSwing"/> degrees; more gusts come, and harder, the more fury there is.
        /// </summary>
        public static Vector3 Gust(Vector2 centre, Vector3 position, float fury, int severity, int seed, double time, StormConfig config)
        {
            float share = GustShare(fury, seed, time, config);
            if (share <= 0f) return Vector3.zero;

            int slot = GustSlot(time, config);
            float swing = (StormProcessor.Hash01(seed, slot, 21) * 2f - 1f) * config.GustSwing;
            Vector3 way = Quaternion.AngleAxis(swing, Vector3.up) * Wind(centre, position);
            return way * (config.GustSpeed * SeverityScale(severity) * Mathf.Sqrt(Mathf.Clamp01(fury)) * share);
        }

        /// <summary>How much of a gust is blowing at <paramref name="time"/>, 0..1 (its rise and fall; 0 with none).</summary>
        public static float GustShare(float fury, int seed, double time, StormConfig config)
        {
            if (fury <= 0f || config.GustSeconds <= 0f) return 0f;

            int slot = GustSlot(time, config);
            double into = time - (double)slot * SlotSeconds(config);
            if (into >= config.GustSeconds) return 0f;
            if (StormProcessor.Hash01(seed, slot, 20) >= config.GustChance * Mathf.Clamp01(fury)) return 0f;

            float rise = Mathf.Sin((float)(into / config.GustSeconds) * Mathf.PI);
            return rise * rise;
        }

        /// <summary>Debris per second the storm throws at a ship here.</summary>
        public static float DebrisRate(float fury, int severity, StormConfig config) =>
            config.DebrisPerMinute / 60f * SeverityScale(severity) * Mathf.Clamp01(fury);

        /// <summary>
        /// Where a piece of debris starts and how it flies, to pass <paramref name="miss"/> (metres off, across the wind
        /// and up) from where the ship will be: <see cref="StormConfig.DebrisDistance"/> (or <paramref name="distance"/>)
        /// upwind of it, carried at the debris speed.
        /// </summary>
        public static (Vector3 Start, Vector3 Velocity, float Lifetime) Debris(Vector2 centre, Vector3 ship, Vector3 shipVelocity,
            Vector2 miss, StormConfig config, float? distance = null)
        {
            Vector3 wind = Wind(centre, ship);
            float upwind = distance ?? config.DebrisDistance;
            float speed = Mathf.Max(1f, config.DebrisSpeed);
            float flight = upwind / speed;
            Vector3 across = Vector3.Cross(Vector3.up, wind);
            Vector3 aim = ship + shipVelocity * flight + across * miss.x + Vector3.up * miss.y;
            return (aim - wind * upwind, wind * speed, 2f * flight);
        }

        private static int GustSlot(double time, StormConfig config) => (int)System.Math.Floor(time / SlotSeconds(config));

        private static double SlotSeconds(StormConfig config) => Mathf.Max(config.GustSeconds, config.GustSlotSeconds);
    }
}
