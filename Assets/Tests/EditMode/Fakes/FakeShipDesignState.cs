#nullable enable
using System;
using TinCan.Core.Ship;
using TinCan.Features.ShipDesigns;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A ship's design state without NGO: <see cref="ServerSetDesign"/> stores what a server would replicate.</summary>
    public sealed class FakeShipDesignState : IShipDesignState
    {
        public FakeShipDesignState(IAirshipView? ship) => Ship = ship;

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true;
        public IAirshipView? Ship { get; set; }
        public ulong DesignHash { get; private set; }
        public byte[] DesignBytes { get; private set; } = Array.Empty<byte>();
        public int Writes { get; private set; }

        public void ServerSetDesign(ulong hash, byte[] bytes)
        {
            DesignHash = hash;
            DesignBytes = bytes;
            Writes++;
        }

        public void Set(ShipDesign design) => ServerSetDesign(ShipDesignHash.Compute(design), ShipDesignBinaryCodec.Encode(design));
    }
}
