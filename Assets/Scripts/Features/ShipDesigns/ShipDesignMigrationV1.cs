#nullable enable
using Newtonsoft.Json.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Version 1 to 2: <c>nextPartInstanceId</c> becomes <c>nextId</c>, since frame elements now share the counter. A
    /// version 1 file has no frame, and nothing else changes. Golden file: <c>v1-golden.ship.json</c>.
    /// </summary>
    public sealed class ShipDesignMigrationV1 : IShipDesignMigration
    {
        public int FromVersion => 1;

        public void Migrate(JObject file)
        {
            if (!file.TryGetValue(ShipDesignFormat.V1NextPartInstanceIdField, out var nextId)) return;

            file.Remove(ShipDesignFormat.V1NextPartInstanceIdField);
            file[ShipDesignFormat.NextIdField] = nextId;
        }
    }
}
