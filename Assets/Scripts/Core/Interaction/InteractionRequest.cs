#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Targeting;
using UnityEngine;

namespace TinCan.Core.Interaction
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
    /// Adapter contract for a world object with a configured interaction binding. Every interaction target is also a
    /// targeting target: the default members below aim at the component's transform, count it while it is active, and
    /// expose the nearest GAS controller for tag filters. So interacting is "targeting with TD_Interact", and no
    /// target component needs its own targeting code.
    /// </summary>
    public interface IInteractionTarget : IInteractable, ITargetable
    {
        InteractionDefinition Definition { get; }

        Vector3 ITargetable.AimPoint => this is Component component && component != null ? component.transform.position : default;

        bool ITargetable.IsTargetable => this is Behaviour behaviour && behaviour != null && behaviour.isActiveAndEnabled;

        IAbilityControllerBase? ITargetable.Controller =>
            this is Component component && component != null ? component.GetComponentInParent<IAbilityControllerBase>() : null;
    }

    public interface IInteractionTargetResolver
    {
        bool TryResolve(InteractionTargetId targetId, out IInteractable? target);
    }
}
