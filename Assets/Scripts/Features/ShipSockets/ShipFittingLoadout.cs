#nullable enable
using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Ship.Sockets;

namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// What a ship design carries in its sockets: one fitting per socket, kept in its own file next to the design
    /// (<see cref="ShipFittingLoadoutFormat"/>), so the design never knows about fittings. Sockets are named by the
    /// part's instance id, which is stable in the design. Immutable; kept in socket order, so a loadout always encodes to
    /// the same text. Plan: .docs/plans/ship-fitting-loadouts.md.
    /// </summary>
    public sealed class ShipFittingLoadout
    {
        public ShipFittingLoadout(IEnumerable<ShipFittingPlacement> fittings)
        {
            Fittings = fittings.OrderBy(f => f.Socket.PartInstanceId).ThenBy(f => f.Socket.Index).ToList();
        }

        public static ShipFittingLoadout Empty { get; } = new(System.Array.Empty<ShipFittingPlacement>());

        public IReadOnlyList<ShipFittingPlacement> Fittings { get; }

        /// <summary>The loadout of what is mounted now, for saving a fitted-out ship.</summary>
        public static ShipFittingLoadout From(IEnumerable<MountedFitting> mounted) =>
            new(mounted.Select(m => new ShipFittingPlacement(m.Socket, m.FittingId.ToString())));

        public override string ToString() => $"{Fittings.Count} fitting(s)";
    }

    /// <summary>One fitting in one socket of a design.</summary>
    public readonly struct ShipFittingPlacement
    {
        public ShipFittingPlacement(ShipSocketId socket, string fittingId)
        {
            Socket = socket;
            FittingId = fittingId;
        }

        public ShipSocketId Socket { get; }
        public string FittingId { get; }

        public override string ToString() => $"{FittingId} in {Socket}";
    }
}
