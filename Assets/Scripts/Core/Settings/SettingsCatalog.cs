#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Domain.Settings;

namespace TinCan.Core.Settings
{
    /// <summary>
    /// Every setting this scene has: the core list on the settings installer plus what the loaded features contribute
    /// (<c>FeatureInstaller.IExtension&lt;SettingDefinition&gt;</c>), in that order.
    /// </summary>
    public sealed class SettingsCatalog
    {
        public SettingsCatalog(IEnumerable<SettingDefinition> definitions)
        {
            All = definitions.Where(d => d != null).Distinct().ToList();
        }

        public IReadOnlyList<SettingDefinition> All { get; }

        public static SettingsCatalog From(IEnumerable<SettingDefinition> core, FeatureInstallerCatalog? features) =>
            new(core.Concat(features == null
                ? Enumerable.Empty<SettingDefinition>()
                : features.Installers.OfType<FeatureInstaller.IExtension<SettingDefinition>>().SelectMany(e => e.Contributions)));
    }
}
