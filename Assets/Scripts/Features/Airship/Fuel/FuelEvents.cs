#nullable enable
using System;

namespace TinCan.Features.Airship.Fuel
{
    public readonly struct FuelEmptyEvent
    {
        public readonly Guid AirshipId;
        public FuelEmptyEvent(Guid airshipId) => AirshipId = airshipId;
        public override string ToString() => $"Airship {AirshipId} ran out of fuel";
    }

    public readonly struct FuelRestoredEvent
    {
        public readonly Guid AirshipId;
        public FuelRestoredEvent(Guid airshipId) => AirshipId = airshipId;
        public override string ToString() => $"Airship {AirshipId} has fuel again";
    }

    public readonly struct JerryCanCaughtEvent
    {
        public readonly Guid CatcherId;
        public readonly int SupplyCount;
        public JerryCanCaughtEvent(Guid catcherId, int supplyCount)
        {
            CatcherId = catcherId;
            SupplyCount = supplyCount;
        }
        public override string ToString() => $"Actor {CatcherId} caught a jerry can (supply now {SupplyCount})";
    }
}
