#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
using TinCan.Core.Domain;

namespace TinCan.Core.RandomStreams
{
    /// <summary>
    /// <see cref="IRandomSource"/> from the launch arguments: <c>-seed &lt;n&gt;</c> (command line or the MPPM tag
    /// <c>seed:n</c>) makes every stream repeatable, seeded with n mixed with a stable hash of the stream's name.
    /// </summary>
    public sealed class RandomSource : IRandomSource
    {
        public const string SeedFlag = "-seed";

        public int? Seed { get; }

        public RandomSource(int? seed) => Seed = seed;

        public static RandomSource FromLaunchArguments(IReadOnlyList<string> args) =>
            new(LaunchArguments.TryGetValue(args, SeedFlag, out var value) &&
                int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed)
                ? seed
                : null);

        public Random Create(string stream) => Seed is { } seed ? new Random(seed ^ StableHash(stream)) : new Random();

        /// <summary>FNV-1a over the name: string.GetHashCode may differ between processes and runtimes.</summary>
        public static int StableHash(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in text)
                {
                    hash ^= c;
                    hash *= 16777619;
                }
                return (int)hash;
            }
        }
    }
}
