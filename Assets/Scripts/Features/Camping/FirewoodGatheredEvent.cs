#nullable enable
using System;

namespace TinCan.Features.Camping
{
    public readonly struct FirewoodGatheredEvent
    {
        public readonly Guid RequesterId;
        public readonly int Left;

        public FirewoodGatheredEvent(Guid requesterId, int left)
        {
            RequesterId = requesterId;
            Left = left;
        }

        public override string ToString() => $"Actor {RequesterId} gathered firewood ({Left} left in the pile)";
    }
}
