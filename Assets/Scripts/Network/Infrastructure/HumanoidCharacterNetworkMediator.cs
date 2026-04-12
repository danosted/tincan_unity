using Unity.Netcode;
using UnityEngine;
using TinCan.Features.HumanoidMovement;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using System;
using VContainer;

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
            return !OwnerId.HasValue || OwnerId == playerId;
        }

        public override void OnNetworkSpawn()
        {
            // Base handles registration with IActorRegistry
            base.OnNetworkSpawn();

            _movement = GetComponent<HumanoidControllerView>();
            _look = GetComponent<ThirdPersonLookView>();
            _receivers = GetComponentsInChildren<IPossessionReceiver>(true);

            _netOwnerId.OnValueChanged += OnOwnerChanged;

            // Initial sync
            if (OwnerId.HasValue) NotifyPossessionChanged(OwnerId.Value, true);
        }

        public override void OnNetworkDespawn()
        {
            _netOwnerId.OnValueChanged -= OnOwnerChanged;
            base.OnNetworkDespawn();
        }

        private void OnOwnerChanged(ulong previous, ulong current)
        {
            if (current != ulong.MaxValue) NotifyPossessionChanged(current, true);
            else NotifyPossessionChanged(previous, false);
        }

        private void NotifyPossessionChanged(ulong playerId, bool possessed)
        {
            foreach (var receiver in _receivers)
            {
                if (receiver == (IPossessionReceiver)this) continue;
                if (possessed) receiver.OnPossessed(playerId);
                else receiver.OnUnpossessed();
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner) UpdateToNetwork();
            else UpdateFromNetwork();
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

        public void OnPossessed(ulong playerId)
        {
            if (IsServer)
            {
                _netOwnerId.Value = playerId;
                _netPossessorId.Value = playerId;
            }
            else
            {
                RequestPossessionServerRpc(playerId);
            }
        }

        public void OnUnpossessed()
        {
            if (IsServer)
            {
                _netOwnerId.Value = ulong.MaxValue;
                _netPossessorId.Value = ulong.MaxValue;
            }
            else
            {
                RequestUnpossessionServerRpc();
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestPossessionServerRpc(ulong playerId)
        {
            if (_netOwnerId.Value == ulong.MaxValue || _netOwnerId.Value == playerId)
            {
                _netOwnerId.Value = playerId;
                _netPossessorId.Value = playerId;
                Debug.Log($"[HumanoidCharacterNetworkMediator] Server updated possession for player {playerId} via RPC");
            }
            else
            {
                Debug.LogWarning($"[HumanoidCharacterNetworkMediator] Player {playerId} tried to possess {gameObject.name} but it is already owned by {_netOwnerId.Value}");
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestUnpossessionServerRpc()
        {
            _netOwnerId.Value = ulong.MaxValue;
            _netPossessorId.Value = ulong.MaxValue;
        }

        public void Dispose() { }
    }
}
