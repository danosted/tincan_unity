#nullable enable
using System;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Pure fire math. A fire is the time it goes out (server time): it burns down by itself with no tick, and every
    /// peer works out what is left from the same replicated value.
    /// </summary>
    public static class FireProcessor
    {
        /// <summary>Seconds of burning left at <paramref name="now"/>.</summary>
        public static float Remaining(double now, double outAt) => (float)Math.Max(0d, outAt - now);

        /// <summary>How full the fire is, 0 (out) to 1 (as full as it gets).</summary>
        public static float Fullness(float remaining, float maxSeconds) =>
            maxSeconds <= 0f ? 0f : Math.Clamp(remaining / maxSeconds, 0f, 1f);

        /// <summary>
        /// Adds a log's worth of burning, capped at <paramref name="maxSeconds"/>. A fire with less than half a log of room
        /// refuses it, so a log is never wasted on a fire that is already full: Accepted is then 0 and OutAt unchanged.
        /// </summary>
        public static (double OutAt, float Accepted) Stoke(double now, double outAt, float seconds, float maxSeconds)
        {
            float remaining = Remaining(now, outAt);
            float room = maxSeconds - remaining;
            if (seconds <= 0f || room < seconds * 0.5f) return (outAt, 0f);

            float accepted = Math.Min(seconds, room);
            return (now + remaining + accepted, accepted);
        }
    }
}
