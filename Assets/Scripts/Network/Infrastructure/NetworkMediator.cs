#nullable enable
using System;
using Unity.Netcode;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Base for networking mediators that are actors. It only supplies identity: the actor id comes from the object's
    /// entity (EntityNetworkMediator), so it is the same on every peer. Registration belongs to the entity, possession
    /// to PossessableNetworkMediator, interaction to the targeting-based interaction feature.
    /// </summary>
    public abstract class NetworkMediator : NetworkBehaviour, IActor
    {
        private ActorIdentity? _identity;

        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public virtual bool IsSimulating => IsSpawned;
    }
}
