#nullable enable
using TinCan.Core.Domain.Features;
using TinCan.Core.Ship;
using TinCan.Core.Ship.Fixtures;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>Keeps the fixtures a ship's <see cref="ShipDesignPreset"/> lists off that ship (made for another hull).</summary>
    public sealed class ShipDesignPresetFixtureFilter : IShipFixtureFilter
    {
        public bool ShouldSpawn(IAirshipView airship, ShipFixtureDefinition fixture)
        {
            var transform = airship.Transform;
            var preset = transform != null ? transform.GetComponent<ShipDesignPreset>() : null;
            return preset == null || !preset.KeepsOff(fixture);
        }
    }
}
