#nullable enable
using System;

namespace TinCan.Core.Domain.Events
{
    public readonly struct PossessionChangedEvent
    {
        public readonly ulong ClientId;
        public readonly Guid? PreviousActorId;
        public readonly Guid? NewActorId;

        public PossessionChangedEvent(ulong clientId, Guid? previousActorId, Guid? newActorId)
        {
            ClientId = clientId;
            PreviousActorId = previousActorId;
            NewActorId = newActorId;
        }

        public override string ToString() => $"Client {ClientId}: {PreviousActorId?.ToString() ?? "none"} -> {NewActorId?.ToString() ?? "none"}";
    }

    public readonly struct AbilityActivatedEvent
    {
        public readonly Guid ActorId;
        public readonly string AbilityName;

        public AbilityActivatedEvent(Guid actorId, string abilityName)
        {
            ActorId = actorId;
            AbilityName = abilityName;
        }

        public override string ToString() => $"Actor {ActorId} activated {AbilityName}";
    }

    public readonly struct AbilityEndedEvent
    {
        public readonly Guid ActorId;
        public readonly string AbilityName;

        public AbilityEndedEvent(Guid actorId, string abilityName)
        {
            ActorId = actorId;
            AbilityName = abilityName;
        }

        public override string ToString() => $"Actor {ActorId} ended {AbilityName}";
    }


}
