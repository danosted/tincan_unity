#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TinCan.Core.Domain.Features
{
    /// <summary>
    /// The set of feature installers found for this build, in a deterministic order, plus their networked prefabs.
    /// Domain-specific contributions (e.g. ship fixtures) are read from <see cref="Installers"/> by their own
    /// dedicated catalog rather than surfaced here, so this type stays a general-purpose view of the installer set.
    /// </summary>
    public sealed class FeatureInstallerCatalog
    {
        public const string ResourcesFolder = "Installers";

        public FeatureInstallerCatalog(IEnumerable<FeatureInstaller> installers)
        {
            Installers = Sort(installers);
            NetworkedPrefabs = Installers.SelectMany(i => i.NetworkedPrefabs).Where(p => p != null).Distinct().ToList();
        }

        public IReadOnlyList<FeatureInstaller> Installers { get; }
        public IReadOnlyList<GameObject> NetworkedPrefabs { get; }

        /// <summary>Loads every FeatureInstaller asset under any Resources/Installers folder.</summary>
        public static FeatureInstallerCatalog LoadFromResources() =>
            new(Resources.LoadAll<FeatureInstaller>(ResourcesFolder));

        /// <summary>Restricts the catalog to the installers listed by a scene's FeatureProfile (and any profiles it includes).</summary>
        public static FeatureInstallerCatalog LoadFromProfile(FeatureProfile profile) =>
            new(profile.ResolveInstallers());

        /// <summary>
        /// What a scene runs: every core installer (always, see <see cref="IsCore"/>) plus the features its profile lists.
        /// Without a profile, every installer under Resources/Installers.
        /// </summary>
        public static FeatureInstallerCatalog LoadForScene(FeatureProfile? profile) =>
            profile == null
                ? LoadFromResources()
                : new(Resources.LoadAll<FeatureInstaller>(ResourcesFolder).Where(IsCore).Concat(profile.ResolveInstallers()).Distinct());

        /// <summary>
        /// A core installer belongs to a core system (TinCan.Gas, TinCan.Targeting, ...) and loads in every scene; a
        /// profile never lists it. Decided by assembly, like everything else about core and features: anything outside a
        /// feature (<c>TinCan.Features*</c>), DevTools or test assembly is core.
        /// </summary>
        public static bool IsCore(FeatureInstaller installer)
        {
            var assembly = installer.GetType().Assembly.GetName().Name;
            return assembly.StartsWith("TinCan.", StringComparison.Ordinal)
                   && !assembly.StartsWith("TinCan.Features", StringComparison.Ordinal)
                   && !assembly.StartsWith("TinCan.DevTools", StringComparison.Ordinal)
                   && !assembly.StartsWith("TinCan.Tests", StringComparison.Ordinal);
        }

        public static List<FeatureInstaller> Sort(IEnumerable<FeatureInstaller> installers) =>
            installers
                .Where(i => i != null)
                .OrderBy(i => i.Order)
                .ThenBy(i => i.name, StringComparer.Ordinal)
                .ToList();
    }
}
