using System;
using TinCan.Core.Domain;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// A transport-free request to perform a specific interaction against an actor.
    /// </summary>
    public readonly struct InteractionRequest
    {
        public readonly Guid RequesterActorId;
        public readonly InteractionTargetId TargetId;

        public InteractionRequest(
            Guid requesterActorId,
            InteractionTargetId targetId)
        {
            RequesterActorId = requesterActorId;
            TargetId = targetId;
        }
    }

    /// <summary>
    /// Stable identifier assigned by the interaction transport to a world target.
    /// </summary>
    public readonly struct InteractionTargetId
    {
        public readonly ulong NetworkObjectId;
        public readonly ushort NetworkBehaviourId;

        public InteractionTargetId(ulong networkObjectId, ushort networkBehaviourId)
        {
            NetworkObjectId = networkObjectId;
            NetworkBehaviourId = networkBehaviourId;
        }
    }

    /// <summary>
    /// Adapter contract for a world object with a configured interaction binding.
    /// </summary>
    public interface IInteractionTarget : IInteractable
    {
        InteractionDefinition Definition { get; }
    }

    public interface IInteractionTargetResolver
    {
        bool TryResolve(InteractionTargetId targetId, out IInteractable target);
    }
}
