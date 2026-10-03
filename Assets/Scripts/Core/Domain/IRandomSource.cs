#nullable enable

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Where gameplay gets its random numbers. Each system asks for its own named stream, so one system drawing more or
    /// fewer numbers does not shift another's. Launched with <c>-seed &lt;n&gt;</c> every stream is repeatable (perf runs
    /// compare like with like, see .docs/PERFORMANCE.md); without it every stream is unseeded, as in normal play.
    /// </summary>
    public interface IRandomSource
    {
        /// <summary>The run's seed, or null when unseeded.</summary>
        int? Seed { get; }

        /// <summary>A generator for one system, e.g. "SkyHazards". Ask once and keep it.</summary>
        System.Random Create(string stream);
    }
}
