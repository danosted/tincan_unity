#nullable enable
using TinCan.Core.Items;

namespace TinCan.Features.Airship.Fuel
{
    /// <summary>Pure: what a held item is worth to the tank, in litres: a fuel crystal, a jerry can; anything else 0.</summary>
    public static class FuelFeed
    {
        public static float Litres(ItemDefinition item, FuelConfig config)
        {
            if (config.CrystalItem != null && item == config.CrystalItem) return config.CrystalLitres;
            if (config.JerryCanItem != null && item == config.JerryCanItem) return config.JerryCanLitres;
            return 0f;
        }
    }
}
