#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;

namespace TinCan.Features.Airship.Fixtures
{
    /// <summary>
    /// Ship-fixture view over the installer set: every fixture contributed by an installer that opts into
    /// <see cref="FeatureInstaller.IExtension{T}"/> for <see cref="ShipFixtureDefinition"/>. Kept separate from
    /// <see cref="FeatureInstallerCatalog"/> so that general-purpose type stays free of this domain-specific concept.
    /// </summary>
    public sealed class ShipFixtureCatalog : IShipFixtureCatalog
    {
        public ShipFixtureCatalog(FeatureInstallerCatalog installers)
        {
            Fixtures = installers.Installers
                .OfType<FeatureInstaller.IExtension<ShipFixtureDefinition>>()
                .SelectMany(e => e.Contributions)
                .Where(f => f != null)
                .ToList();
        }

        public IReadOnlyList<ShipFixtureDefinition> Fixtures { get; }
    }
}
