#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Input
{
    /// <summary>
    /// Every context this scene evaluates: the core ones listed in the input config plus those the loaded features
    /// contribute (<c>FeatureInstaller.IExtension&lt;InputContext&gt;</c>), so an unloaded feature's context never runs.
    /// </summary>
    public sealed class InputContextSet
    {
        public InputContextSet(IEnumerable<InputContext> contexts)
        {
            All = contexts.Where(context => context != null).Distinct().ToList();
        }

        public IReadOnlyList<InputContext> All { get; }

        public static InputContextSet From(InputConfig config, FeatureInstallerCatalog? features) =>
            new(config.Contexts.Concat(features == null
                ? Enumerable.Empty<InputContext>()
                : features.Installers.OfType<FeatureInstaller.IExtension<InputContext>>().SelectMany(e => e.Contributions)));
    }
}
