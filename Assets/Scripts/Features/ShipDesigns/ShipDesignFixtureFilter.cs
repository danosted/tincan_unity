#nullable enable
using System.Linq;
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Fixtures;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Keeps installer fixtures off a ship when a part spawns the same prefab: with ship designs loaded, the design places
    /// the helm, so the Helm installer's fixed-pose helm is not spawned as well.
    /// </summary>
    public sealed class ShipDesignFixtureFilter : IShipFixtureFilter
    {
        private readonly IShipPartCatalog _catalog;

        public ShipDesignFixtureFilter(IShipPartCatalog catalog) => _catalog = catalog;

        public bool ShouldSpawn(IAirshipView airship, ShipFixtureDefinition fixture) =>
            fixture.Prefab == null || _catalog.Parts.All(part => part.NetworkedPrefab != fixture.Prefab);
    }
}
