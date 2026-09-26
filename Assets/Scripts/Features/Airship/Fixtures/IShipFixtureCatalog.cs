#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain.Features;

namespace TinCan.Features.Airship.Fixtures
{
    public interface IShipFixtureCatalog
    {
        IReadOnlyList<ShipFixtureDefinition> Fixtures { get; }
    }
}
