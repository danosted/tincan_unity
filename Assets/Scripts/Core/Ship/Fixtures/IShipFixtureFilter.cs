#nullable enable
using TinCan.Core.Domain.Features;

namespace TinCan.Core.Ship.Fixtures
{
    /// <summary>
    /// Lets a feature keep an installer's fixture off a ship: <see cref="ShipFixtureSpawningUseCase"/> spawns a fixture
    /// only if every registered filter agrees. Ship designs use it so a fixture the design places itself (the helm) is not
    /// spawned a second time at its fixed pose. With no filter registered, every fixture spawns.
    /// </summary>
    public interface IShipFixtureFilter
    {
        bool ShouldSpawn(IAirshipView airship, ShipFixtureDefinition fixture);
    }
}
