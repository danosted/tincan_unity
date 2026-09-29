#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;

namespace TinCan.Features.DesignedEvents
{
    /// <summary>
    /// Every designed event the loaded features contribute. A feature authors its events in code next to the handlers
    /// they need (ship damage: <c>ShipDamageDesignedEvents</c>) and lists them through
    /// <c>FeatureInstaller.IExtension&lt;EventDefinition&gt;</c>, so this feature references none of them and a profile
    /// without ship damage simply has no hull events. See .docs/plans/designed-events.md.
    /// </summary>
    public static class EventCatalog
    {
        public static IReadOnlyList<EventDefinition> Collect(FeatureInstallerCatalog features) =>
            features.Installers
                .OfType<FeatureInstaller.IExtension<EventDefinition>>()
                .SelectMany(e => e.Contributions)
                .Where(definition => definition != null)
                .ToList();
    }
}
