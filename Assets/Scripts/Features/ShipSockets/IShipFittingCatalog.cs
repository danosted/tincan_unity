#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>The fittings the loaded features contribute, by <see cref="ShipFittingDefinition.FittingId"/>.</summary>
    public interface IShipFittingCatalog
    {
        IReadOnlyList<ShipFittingDefinition> Fittings { get; }

        bool TryGet(string fittingId, out ShipFittingDefinition fitting);
    }
}
