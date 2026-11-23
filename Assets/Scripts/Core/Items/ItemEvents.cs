#nullable enable
using System;

namespace TinCan.Core.Items
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

    /// <summary>Server: an item came out of a target into a player's hands (ItemInteractionHandler).</summary>
    public readonly struct ItemTakenEvent
    {
        public readonly Guid ActorId;
        public readonly string Item;
        public readonly string Target;

        public ItemTakenEvent(Guid actorId, string item, string target)
        {
            ActorId = actorId;
            Item = item;
            Target = target;
        }
    }

    /// <summary>Server: a held item went back into a target (ItemInteractionHandler).</summary>
    public readonly struct ItemStoredEvent
    {
        public readonly Guid ActorId;
        public readonly string Item;
        public readonly string Target;

        public ItemStoredEvent(Guid actorId, string item, string target)
        {
            ActorId = actorId;
            Item = item;
            Target = target;
        }
    }

    /// <summary>Server: a target used up a held item (ItemInteractionHandler): fuel fed, a fire stoked, a tent pitched.</summary>
    public readonly struct ItemUsedEvent
    {
        public readonly Guid ActorId;
        public readonly string Item;
        public readonly string Target;

        public ItemUsedEvent(Guid actorId, string item, string target)
        {
            ActorId = actorId;
            Item = item;
            Target = target;
        }
    }
}
