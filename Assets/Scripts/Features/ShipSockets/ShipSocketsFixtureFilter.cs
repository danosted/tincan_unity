#nullable enable
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Fixtures;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// With sockets loaded, fittings go in sockets: a feature's fixed-pose fixture whose prefab is also a fitting (the
    /// cannon station) is kept off the ship.
    /// </summary>
    public sealed class ShipSocketsFixtureFilter : IShipFixtureFilter
    {
        private readonly IShipFittingCatalog _catalog;

        public ShipSocketsFixtureFilter(IShipFittingCatalog catalog) => _catalog = catalog;

        public bool ShouldSpawn(IAirshipView airship, ShipFixtureDefinition fixture) =>
            fixture.Prefab == null || _catalog.Fittings.All(f => f.Prefab != fixture.Prefab);
    }
}
