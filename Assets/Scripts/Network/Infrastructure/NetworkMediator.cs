using Unity.Netcode;
using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Features.Interaction;
using VContainer;
using System;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Base class for networking mediators.
    /// Bridges the gap between Domain Use Cases and the Networking Library (NGO).
    /// Handles automatic registration with the IActorRegistry and provides default IPossessable behavior.
    /// </summary>
    public abstract class NetworkMediator : NetworkBehaviour, IPossessable, IInteractionRequester
    {
        public virtual Guid Id { get; } = Guid.NewGuid();
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

        public void RequestInteraction(IInteractable target)
        {
            if (target is not NetworkBehaviour targetNetBhv)
            {
                Debug.LogWarning($"[{GetType().Name}] Interaction target {target.GetType().Name} is not a NetworkBehaviour.");
                return;
            }
            RequestInteractionServerRpc(targetNetBhv);
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractionServerRpc(NetworkBehaviourReference targetRef, RpcParams rpcParams = default)
        {
            if (targetRef.TryGet(out NetworkBehaviour targetNetBhv) &&
                targetNetBhv is IInteractionTarget target &&
                targetNetBhv.GetComponentInParent<NetworkObject>() is { } targetObject &&
                TryGetRequesterActorId(rpcParams.Receive.SenderClientId, out var requesterActorId))
            {
                InteractionOrchestrator.HandleInteraction(new InteractionRequest(
                    requesterActorId,
                    new InteractionTargetId(
                        targetObject.NetworkObjectId,
                        targetNetBhv.NetworkBehaviourId)));
            }
            else
            {
                Debug.LogWarning($"[{GetType().Name}] Interaction target not found or unsupported.");
            }
        }

        private bool TryGetRequesterActorId(ulong clientId, out Guid actorId)
        {
            actorId = Guid.Empty;
            if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var client) ||
                client.PlayerObject == null ||
                !client.PlayerObject.TryGetComponent(out IActor actor))
            {
                return false;
            }

            actorId = actor.Id;
            return true;
        }
    }
}
