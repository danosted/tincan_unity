using Unity.Netcode;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Features.Interaction;
using TinCan.Features.Possession;
using VContainer;
using System;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Base class for networking mediators.
    /// Bridges the gap between Domain Use Cases and the Networking Library (NGO).
    /// Handles automatic registration with the IActorRegistry and provides default IPossessable behavior.
    /// </summary>
    public abstract class NetworkMediator : NetworkBehaviour, IPossessable
    {
        public Guid Id { get; } = Guid.NewGuid();
        public virtual bool IsSimulating => IsSpawned;

        protected IActorRegistry Registry { get; private set; }
        protected IInteractionOrchestrator InteractionOrchestrator { get; private set; }

        [Inject]
        public void Construct(IActorRegistry registry, IInteractionOrchestrator interactionOrchestrator)
        {
            Registry = registry;
            InteractionOrchestrator = interactionOrchestrator;
        }

        public virtual bool CanPossess(ulong playerId)
        {
            // Default: Only allow possession if unowned, or by the current owner.
            // For Player-owned objects (OwnerClientId), we use that as the primary authority.
            if (IsSpawned && OwnerClientId != 0 && OwnerClientId != ulong.MaxValue)
            {
                return OwnerClientId == playerId;
            }

            return true;
        }
    }
}
