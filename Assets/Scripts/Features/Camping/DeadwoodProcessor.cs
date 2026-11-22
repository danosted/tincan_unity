#nullable enable
using System;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Pure deadwood math. A pile is a stock of logs counted at a time; it gathers a log again every regrow interval up
    /// to its size, worked out when read, so it needs no tick. <c>At</c> is where the regrowth counts from: carried over
    /// while the pile grows, restarted when the pile is full.
    /// </summary>
    public static class DeadwoodProcessor
    {
        /// <summary>The stock as it stands at <paramref name="now"/>, and where its next log counts from.</summary>
        public static (int Stock, double At) Settle(int stock, double at, double now, int size, float regrowSeconds)
        {
            if (stock >= size || regrowSeconds <= 0f) return (size, now);

            int grown = (int)Math.Floor((now - at) / regrowSeconds);
            if (grown <= 0) return (Math.Max(0, stock), at);

            int settled = Math.Min(size, Math.Max(0, stock) + grown);
            return settled >= size ? (size, now) : (settled, at + grown * (double)regrowSeconds);
        }

        /// <summary>One log taken, or null when the pile is bare.</summary>
        public static (int Stock, double At)? Take(int stock, double at, double now, int size, float regrowSeconds)
        {
            var settled = Settle(stock, at, now, size, regrowSeconds);
            if (settled.Stock <= 0) return null;
            return (settled.Stock - 1, settled.At);
        }

        /// <summary>One log put back (never more than the pile holds).</summary>
        public static (int Stock, double At) Return(int stock, double at, double now, int size, float regrowSeconds)
        {
            var settled = Settle(stock, at, now, size, regrowSeconds);
            int returned = Math.Min(size, settled.Stock + 1);
            return returned >= size ? (size, now) : (returned, settled.At);
        }
    }
}
