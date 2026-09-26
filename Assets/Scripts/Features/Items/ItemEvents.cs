#nullable enable
using System;

namespace TinCan.Features.Items
{
    /// <summary>Server: an actor's held item changed. Names, not references, so the event logs and serialises cleanly.</summary>
    public readonly struct HeldItemChangedEvent
    {
        public readonly Guid ActorId;
        public readonly string Previous;
        public readonly string Current;

        public HeldItemChangedEvent(Guid actorId, string previous, string current)
        {
            ActorId = actorId;
            Previous = previous;
            Current = current;
        }
    }
}
