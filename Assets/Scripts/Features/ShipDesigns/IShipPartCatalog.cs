#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship.Parts;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>The parts the loaded features contribute, by <see cref="ShipPartDefinition.PartId"/>.</summary>
    public interface IShipPartCatalog
    {
        IReadOnlyList<ShipPartDefinition> Parts { get; }

        bool TryGet(string partId, out ShipPartDefinition part);
    }
}
