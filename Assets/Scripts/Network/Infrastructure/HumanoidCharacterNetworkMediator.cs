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
        private HumanoidControllerView _localMovement;
        private ThirdPersonLookView _localLook;

        private readonly NetworkVariable<Vector3> _netPosition = new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Quaternion> _netRotation = new NetworkVariable<Quaternion>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<ulong> _netOwnerId = new NetworkVariable<ulong>(ulong.MaxValue);
        private readonly NetworkVariable<ulong> _netPossessorId = new NetworkVariable<ulong>(ulong.MaxValue);

        private IPossessionResponder[] _receivers;

        private void Awake()
        {
            _localMovement = GetComponent<HumanoidControllerView>();
            _localLook = GetComponent<ThirdPersonLookView>();
            _receivers = GetComponentsInChildren<IPossessionResponder>(true);
        }

        // IActor Implementation
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => IsOwner; // Only run movement logic on the owner client

        // IPossessable Implementation
        public ulong? OwnerId => _netOwnerId.Value == ulong.MaxValue ? null : _netOwnerId.Value;
        public ulong? PossessorId => _netPossessorId.Value == ulong.MaxValue ? null : _netPossessorId.Value;

        // IHumanoidCharacterView Implementation
        public IHumanoidMovementView Movement => _localMovement;
        public IHumanoidLookView Look => _localLook;

        private void Update()
        {
            if (!IsOwner && IsSpawned)
            {
                UpdateFromNetwork();
            }
        }

        public void OnPossessed(ulong playerId)
        {
            if (IsServer)
            {
                _netOwnerId.Value = playerId;
                _netPossessorId.Value = playerId;
            }

            foreach (var receiver in _receivers)
            {
                if (receiver == (IPossessionResponder)this) continue;
                receiver.OnPossessed();
            }
        }

        public void OnUnpossessed()
        {
            if (IsServer)
            {
                _netPossessorId.Value = ulong.MaxValue;
            }

            foreach (var receiver in _receivers)
            {
                if (receiver == (IPossessionResponder)this) continue;
                receiver.OnUnpossessed();
            }
        }

        private void UpdateFromNetwork()
        {
            transform.position = _netPosition.Value;
            transform.rotation = _netRotation.Value;
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
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
