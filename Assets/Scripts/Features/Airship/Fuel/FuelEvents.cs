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

    public readonly struct FuelRefilledEvent
    {
        public readonly Guid RequesterId;
        public readonly float Accepted;
        public FuelRefilledEvent(Guid requesterId, float accepted)
        {
            RequesterId = requesterId;
            Accepted = accepted;
        }
        public override string ToString() => $"Actor {RequesterId} refilled {Accepted:0.#} fuel";
    }

    public readonly struct JerryCanTakenEvent
    {
        public readonly Guid RequesterId;
        public readonly int Remaining;
        public JerryCanTakenEvent(Guid requesterId, int remaining)
        {
            RequesterId = requesterId;
            Remaining = remaining;
        }
        public override string ToString() => $"Actor {RequesterId} took a jerry can ({Remaining} left)";
    }

    public readonly struct JerryCanReturnedEvent
    {
        public readonly Guid RequesterId;
        public readonly int Remaining;
        public JerryCanReturnedEvent(Guid requesterId, int remaining)
        {
            RequesterId = requesterId;
            Remaining = remaining;
        }
        public override string ToString() => $"Actor {RequesterId} returned a jerry can ({Remaining} in supply)";
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
