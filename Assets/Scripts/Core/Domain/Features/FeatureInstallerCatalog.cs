#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace TinCan.Core.Domain.Features
{
    /// <summary>
    /// The set of feature installers found for this build, in a deterministic order, plus everything they contribute.
    /// </summary>
    public sealed class FeatureInstallerCatalog : IShipFixtureCatalog
    {
        public const string ResourcesFolder = "Installers";

        public FeatureInstallerCatalog(IEnumerable<FeatureInstaller> installers)
        {
            var _installers = installers;
            if (!_installers.Any())
            {
                _installers = Resources.LoadAll<FeatureInstaller>(ResourcesFolder);
            }
            Installers = Sort(_installers);
            Fixtures = Installers.SelectMany(i => i.ShipFixtures).Where(f => f != null).ToList();
            NetworkedPrefabs = Installers.SelectMany(i => i.NetworkedPrefabs).Where(p => p != null).Distinct().ToList();
        }

        public IReadOnlyList<FeatureInstaller> Installers { get; }
        public IReadOnlyList<ShipFixtureDefinition> Fixtures { get; }
        public IReadOnlyList<GameObject> NetworkedPrefabs { get; }

        /// <summary>Loads every FeatureInstaller asset under any Resources/Installers folder.</summary>
        public static FeatureInstallerCatalog LoadFromResources() =>
            new(Resources.LoadAll<FeatureInstaller>(ResourcesFolder));

        /// <summary>Restricts the catalog to the installers listed by a scene's FeatureProfile (and any profiles it includes).</summary>
        public static FeatureInstallerCatalog LoadFromProfile(FeatureProfile profile) =>
            new(profile.ResolveInstallers());

        public static List<FeatureInstaller> Sort(IEnumerable<FeatureInstaller> installers) =>
            installers
                .Where(i => i != null)
                .OrderBy(i => i.Order)
                .ThenBy(i => i.name, StringComparer.Ordinal)
                .ToList();
    }
}
