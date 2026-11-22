#nullable enable
using System;

namespace TinCan.Features.Camping
{
    public readonly struct TentPackedEvent
    {
        public readonly Guid RequesterId;

        public TentPackedEvent(Guid requesterId) => RequesterId = requesterId;

        public override string ToString() => $"Actor {RequesterId} packed up a tent";
    }
}
