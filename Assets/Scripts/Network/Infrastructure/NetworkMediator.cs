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
        protected IActorOrchestrator ActorOrchestrator { get; private set; }

        private struct OptionalClientId : INetworkSerializable
        {
            public bool HasValue;
            public ulong Value;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref HasValue);
                if (HasValue)
                {
                    serializer.SerializeValue(ref Value);
                }
            }
        }

        private NetworkVariable<OptionalClientId> _possessorId = new NetworkVariable<OptionalClientId>(
            new OptionalClientId { HasValue = false, Value = 0 },
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public ulong? PossessorId => _possessorId.Value.HasValue ? _possessorId.Value.Value : (ulong?)null;

        [Inject]
        public void Construct(IActorRegistry registry, IInteractionOrchestrator interactionOrchestrator, IActorOrchestrator actorOrchestrator)
        {
            Registry = registry;
            InteractionOrchestrator = interactionOrchestrator;
            ActorOrchestrator = actorOrchestrator;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            // If this is a player's primary character, they inherently possess it upon spawn.
            if (IsServer && NetworkObject.IsPlayerObject)
            {
                AuthoritativeSetPossessor(OwnerClientId);
            }

            ActorOrchestrator?.RegisterHierarchy(gameObject);
        }

        public override void OnNetworkDespawn()
        {
            ActorOrchestrator?.UnregisterHierarchy(gameObject);
            base.OnNetworkDespawn();
        }

        public virtual bool CanPossess(ulong playerId)
        {
            if (!IsSpawned) return false;

            if (_possessorId.Value.HasValue)
            {
                return _possessorId.Value.Value == playerId;
            }

            return true;
        }

        public void AuthoritativeSetPossessor(ulong? playerId)
        {
            if (!IsServer) return;

            if (playerId.HasValue)
            {
                _possessorId.Value = new OptionalClientId { HasValue = true, Value = playerId.Value };
            }
            else
            {
                _possessorId.Value = new OptionalClientId { HasValue = false, Value = 0 };
            }
        }
    }
}
