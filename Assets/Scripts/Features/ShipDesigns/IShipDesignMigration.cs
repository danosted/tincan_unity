#nullable enable
using Newtonsoft.Json.Linq;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Rewrites a design file from <see cref="FromVersion"/> to the next version, in place. One per format change; each
    /// ships with a sample file of its old version in the tests.
    /// </summary>
    public interface IShipDesignMigration
    {
        int FromVersion { get; }

        void Migrate(JObject file);
    }
}
