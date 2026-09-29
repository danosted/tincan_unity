#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;

namespace TinCan.Core.Ship.Fixtures
{
    public interface IShipFixtureCatalog
    {
        IReadOnlyList<ShipFixtureDefinition> Fixtures { get; }
    }
}
