#nullable enable
using Newtonsoft.Json.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Version 2 to 3: each part's grid <c>cell</c> (whole metres) becomes <c>pos</c> in frame units, ten times it, so a
    /// migrated ship is built exactly where it was. Golden file: <c>v2-golden.ship.json</c>.
    /// </summary>
    public sealed class ShipDesignMigrationV2 : IShipDesignMigration
    {
        public int FromVersion => 2;

        public void Migrate(JObject file)
        {
            if (file[ShipDesignFormat.PartsField] is not JArray parts) return;

            foreach (var token in parts)
            {
                if (token is not JObject part || part[ShipDesignFormat.V2PartCellField] is not JArray cell) continue;

                var pos = new JArray();
                foreach (var value in cell)
                {
                    // Anything but a whole number is left as it is, for the decoder to report.
                    pos.Add(value.Type == JTokenType.Integer ? new JValue(value.Value<long>() * ShipFrameUnits.PerMetre) : value);
                }

                part.Remove(ShipDesignFormat.V2PartCellField);
                part[ShipDesignFormat.V3PartPosField] = pos;
            }
        }
    }
}
