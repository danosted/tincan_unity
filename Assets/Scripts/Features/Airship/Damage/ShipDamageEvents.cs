#nullable enable
using System;

namespace TinCan.Features.Airship.Damage
{
    /// <summary>Server: a part of the ship broke.</summary>
    public readonly struct ShipPartBrokenEvent
    {
        public readonly Guid AirshipId;
        public readonly int PointIndex;
        public readonly int BrokenCount;

        public ShipPartBrokenEvent(Guid airshipId, int pointIndex, int brokenCount)
        {
            AirshipId = airshipId;
            PointIndex = pointIndex;
            BrokenCount = brokenCount;
        }
    }

    /// <summary>Server: a broken part is fully repaired.</summary>
    public readonly struct ShipPartRepairedEvent
    {
        public readonly Guid AirshipId;
        public readonly int PointIndex;
        public readonly int BrokenCount;

        public ShipPartRepairedEvent(Guid airshipId, int pointIndex, int brokenCount)
        {
            AirshipId = airshipId;
            PointIndex = pointIndex;
            BrokenCount = brokenCount;
        }
    }

    /// <summary>Server: a player's repair tool applied one repair effect to a broken part (its health is on the part's controller).</summary>
    public readonly struct ShipPartRepairTickEvent
    {
        public readonly Guid RepairerId;
        public readonly int PointIndex;

        public ShipPartRepairTickEvent(Guid repairerId, int pointIndex)
        {
            RepairerId = repairerId;
            PointIndex = pointIndex;
        }
    }
}
