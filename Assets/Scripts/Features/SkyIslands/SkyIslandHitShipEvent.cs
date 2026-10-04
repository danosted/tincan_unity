#nullable enable
using System;

namespace TinCan.Features.SkyIslands
{
    /// <summary>The ship hit an island hard enough to hurt (once per impact cooldown). <see cref="Hits"/> counts them all.</summary>
    public readonly struct SkyIslandHitShipEvent
    {
        public readonly Guid ShipId;
        public readonly SkyIslandId Island;
        public readonly int Hits;

        public SkyIslandHitShipEvent(Guid shipId, SkyIslandId island, int hits)
        {
            ShipId = shipId;
            Island = island;
            Hits = hits;
        }

        public override string ToString() => $"SkyIslandHitShip(ship {ShipId}, island {Island}, hits {Hits})";
    }
}
