#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Features.Camping
{
    /// <summary>
    /// Pure deadwood math: a pile is a <see cref="RegrowingStock"/> of logs (the same stock a crystal vein is).
    /// </summary>
    public static class DeadwoodProcessor
    {
        /// <summary>The stock as it stands at <paramref name="now"/>, and where its next log counts from.</summary>
        public static (int Stock, double At) Settle(int stock, double at, double now, int size, float regrowSeconds) =>
            RegrowingStock.Settle(stock, at, now, size, regrowSeconds);

        /// <summary>One log taken, or null when the pile is bare.</summary>
        public static (int Stock, double At)? Take(int stock, double at, double now, int size, float regrowSeconds) =>
            RegrowingStock.Take(stock, at, now, size, regrowSeconds);

        /// <summary>One log put back (never more than the pile holds).</summary>
        public static (int Stock, double At) Return(int stock, double at, double now, int size, float regrowSeconds) =>
            RegrowingStock.Return(stock, at, now, size, regrowSeconds);
    }
}
