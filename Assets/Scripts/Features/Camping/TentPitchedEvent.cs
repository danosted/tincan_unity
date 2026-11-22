#nullable enable
using System;

namespace TinCan.Features.Camping
{
    public readonly struct TentPitchedEvent
    {
        public readonly Guid RequesterId;

        public TentPitchedEvent(Guid requesterId) => RequesterId = requesterId;

        public override string ToString() => $"Actor {RequesterId} pitched a tent";
    }
}
