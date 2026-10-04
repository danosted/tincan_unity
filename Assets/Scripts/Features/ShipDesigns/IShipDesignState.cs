#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Ship;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// The design a ship is built from, replicated to every peer (late joiners included) by the ShipDesignState fixture
    /// on that ship. Only the server writes it. A hash of 0 means no design yet.
    /// </summary>
    public interface IShipDesignState : IActor
    {
        /// <summary>The ship this state is fixed to, once its parent has replicated.</summary>
        IAirshipView? Ship { get; }

        ulong DesignHash { get; }

        /// <summary>The design in its network form (<see cref="ShipDesignBinaryCodec"/>).</summary>
        byte[] DesignBytes { get; }

        void ServerSetDesign(ulong hash, byte[] bytes);
    }
}
