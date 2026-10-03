#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Domain.Features;
using UnityEngine;
using VContainer;

namespace TinCan.Core.RandomStreams
{
    /// <summary>
    /// Core, always loaded (a core installer; profiles never list it): registers <see cref="IRandomSource"/> from the
    /// launch arguments, so <c>-seed &lt;n&gt;</c> makes every gameplay random stream repeatable (perf runs,
    /// .docs/PERFORMANCE.md). Runs early: feature use cases take the source in their constructors.
    /// </summary>
    [CreateAssetMenu(fileName = "RandomSourceFeatureInstaller", menuName = "TinCan/Features/Random Source Installer")]
    public class RandomSourceFeatureInstaller : FeatureInstaller
    {
        public override int Order => -30;

        public override void Install(IContainerBuilder builder)
        {
            var source = RandomSource.FromLaunchArguments(LaunchArguments.Current);
            if (source.Seed is { } seed) Debug.Log($"[Random] Seeded run: every gameplay random stream derives from seed {seed}.");
            builder.RegisterInstance<IRandomSource>(source);
        }
    }
}
