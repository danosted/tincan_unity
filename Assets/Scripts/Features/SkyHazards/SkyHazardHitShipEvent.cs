#nullable enable
using System;

namespace TinCan.Features.SkyHazards
{
    /// <summary>Server: a hazard reached the ship and hurt it.</summary>
    public readonly struct SkyHazardHitShipEvent
    {
        public readonly Guid AirshipId;
        public readonly int Hits;

        public SkyHazardHitShipEvent(Guid airshipId, int hits)
        {
            AirshipId = airshipId;
            Hits = hits;
        }
    }
}
