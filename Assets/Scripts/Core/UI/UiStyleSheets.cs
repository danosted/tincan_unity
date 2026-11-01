#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;
using UnityEngine.UIElements;

namespace TinCan.Core.UI
{
    /// <summary>
    /// The component sheets the loaded features bring (<c>FeatureInstaller.IExtension&lt;StyleSheet&gt;</c>), added to the
    /// layers' root by <see cref="UiLayers"/>. Core components are in the theme (Assets/UI/Theme/TinCan.tss), so a
    /// feature's styles exist only where the feature is loaded and core never names a feature's sheet.
    /// </summary>
    public sealed class UiStyleSheets
    {
        public UiStyleSheets(IEnumerable<StyleSheet> sheets)
        {
            All = sheets.Where(sheet => sheet != null).Distinct().ToList();
        }

        public IReadOnlyList<StyleSheet> All { get; }

        public static UiStyleSheets From(FeatureInstallerCatalog? features) =>
            new(features == null
                ? Enumerable.Empty<StyleSheet>()
                : features.Installers.OfType<FeatureInstaller.IExtension<StyleSheet>>().SelectMany(e => e.Contributions));
    }
}
