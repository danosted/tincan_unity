#nullable enable
using System;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Pure: a stock of things in the world that grows back (deadwood's logs, a vein's crystals). It is a count at a time
    /// and grows one back every regrow interval up to its size, worked out when read, so it needs no tick. <c>At</c> is
    /// where the regrowth counts from: carried over while the stock grows, restarted when it is full.
    /// </summary>
    public static class RegrowingStock
    {
        /// <summary>The stock as it stands at <paramref name="now"/>, and where its next one counts from.</summary>
        public static (int Stock, double At) Settle(int stock, double at, double now, int size, float regrowSeconds)
        {
            if (stock >= size || regrowSeconds <= 0f) return (size, now);

            int grown = (int)Math.Floor((now - at) / regrowSeconds);
            if (grown <= 0) return (Math.Max(0, stock), at);

            int settled = Math.Min(size, Math.Max(0, stock) + grown);
            return settled >= size ? (size, now) : (settled, at + grown * (double)regrowSeconds);
        }

        /// <summary>One taken, or null when the stock is bare.</summary>
        public static (int Stock, double At)? Take(int stock, double at, double now, int size, float regrowSeconds)
        {
            var settled = Settle(stock, at, now, size, regrowSeconds);
            if (settled.Stock <= 0) return null;
            return (settled.Stock - 1, settled.At);
        }

        /// <summary>One put back (never more than the stock holds).</summary>
        public static (int Stock, double At) Return(int stock, double at, double now, int size, float regrowSeconds)
        {
            var settled = Settle(stock, at, now, size, regrowSeconds);
            int returned = Math.Min(size, settled.Stock + 1);
            return returned >= size ? (size, now) : (returned, settled.At);
        }
    }
}
