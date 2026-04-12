using Unity.Netcode;
using UnityEngine;
using TinCan.Features.HumanoidMovement;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using System;
using VContainer;
using VContainer.Unity;

namespace TinCan.Network.Infrastructure
{
    /// <summary>
    /// Mediator that wraps a complete Humanoid character to provide networking capabilities.
    /// Bridges the local domain logic with the network state at the "Face" level.
    /// </summary>
    [RequireComponent(typeof(HumanoidControllerView))]
    [RequireComponent(typeof(ThirdPersonLookView))]
    public class HumanoidCharacterNetworkMediator : NetworkMediator, IHumanoidCharacterView
    {
        private HumanoidControllerView _movement;
        private ThirdPersonLookView _look;
        private IActorRegistry _registry;

        [Inject]
        public void Construct(IActorRegistry registry)
        {
            _registry = registry;
            Debug.Log($"[HumanoidCharacterNetworkMediator] Injected via VContainer for {gameObject.name}");
        }

        private readonly NetworkVariable<Vector3> _netPosition = new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ulong> _netOwnerId = new NetworkVariable<ulong>(ulong.MaxValue);
        private readonly NetworkVariable<ulong> _netPossessorId = new NetworkVariable<ulong>(ulong.MaxValue);

        private IPossessionReceiver[] _receivers = Array.Empty<IPossessionReceiver>();

        // IActor Implementation
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => IsOwner; // Only run movement logic on the owner client

        // IPossessable Implementation
        public ulong? OwnerId => _netOwnerId.Value == ulong.MaxValue ? null : _netOwnerId.Value;
        public ulong? PossessorId => _netPossessorId.Value == ulong.MaxValue ? null : _netPossessorId.Value;

        // IHumanoidCharacterView Implementation
        public IHumanoidMovementView Movement => _movement;
        public IHumanoidLookView Look => _look;

        public bool CanPossess(ulong playerId)
        {
            // Allow if unowned, or if we are already the owner
            return !OwnerId.HasValue || OwnerId == playerId;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                UpdateToNetwork();
            }
            else
            {
                UpdateFromNetwork();
            }
        }

        public void OnPossessed(ulong playerId)
        {
            Debug.Log($"[HumanoidCharacterNetworkMediator] OnPossessed by player {playerId} (IsOwner: {IsOwner}, IsServer: {IsServer})");

            if (IsServer)
            {
                _netOwnerId.Value = playerId;
                _netPossessorId.Value = playerId;
            }
            else if (IsOwner)
            {
                // If we are the owner (client), we notify the server to update the shared state
                RequestPossessionServerRpc(playerId);
            }

            foreach (var receiver in _receivers)
            {
                if (receiver == (IPossessionReceiver)this) continue;
                receiver.OnPossessed(playerId);
            }
        }

        public void OnUnpossessed()
        {
            if (IsServer)
            {
                _netPossessorId.Value = ulong.MaxValue;
            }
            else if (IsOwner)
            {
                RequestUnpossessionServerRpc();
            }

            foreach (var receiver in _receivers)
            {
                if (receiver == (IPossessionReceiver)this) continue;
                receiver.OnUnpossessed();
            }
        }

        [ServerRpc]
        private void RequestPossessionServerRpc(ulong playerId)
        {
            _netOwnerId.Value = playerId;
            _netPossessorId.Value = playerId;
            Debug.Log($"[HumanoidCharacterNetworkMediator] Server updated possession for player {playerId} via RPC");
        }

        [ServerRpc]
        private void RequestUnpossessionServerRpc()
        {
            _netPossessorId.Value = ulong.MaxValue;
        }

        private void UpdateToNetwork()
        {
            _netPosition.Value = transform.position;
            _netRotation.Value = transform.rotation;
        }

        private void UpdateFromNetwork()
        {
            transform.position = _netPosition.Value;
            transform.rotation = _netRotation.Value;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[HumanoidCharacterNetworkMediator] OnNetworkSpawn - IsOwner: {IsOwner}, NetworkObjectId: {NetworkObjectId}");

            // 1. Discover components
            _movement = GetComponent<HumanoidControllerView>();
            _look = GetComponent<ThirdPersonLookView>();
            _receivers = GetComponentsInChildren<IPossessionReceiver>(true);

            // 2. Register with actor system (trusting that _registry is injected)
            if (_registry != null)
            {
                _registry.Register(this);
            }
            else
            {
                Debug.LogError($"[HumanoidCharacterNetworkMediator] CRITICAL: IActorRegistry is null on {gameObject.name}. Did injection fail?");
            }
        }

        public void Dispose()
        {
            // Registration is managed by LifetimeScope BuildCallback
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
        }
    }
}
