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

        private readonly NetworkVariable<HumanoidInputState> _netInputState = new NetworkVariable<HumanoidInputState>(
            writePerm: NetworkVariableWritePermission.Owner);

        private readonly NetworkVariable<NetworkObjectReference> _netParentPlatform = new NetworkVariable<NetworkObjectReference>(
            writePerm: NetworkVariableWritePermission.Owner);
        private readonly NetworkVariable<Vector3> _netLocalPosition = new NetworkVariable<Vector3>(
            writePerm: NetworkVariableWritePermission.Owner);

        private IPossessionReceiver[] _receivers = Array.Empty<IPossessionReceiver>();

        // IActor Implementation
        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating => true; // All clients simulate all characters locally based on input

        // IPossessable Implementation
        public ulong? OwnerId => _netOwnerId.Value == ulong.MaxValue ? null : _netOwnerId.Value;
        public ulong? PossessorId => _netPossessorId.Value == ulong.MaxValue ? null : _netPossessorId.Value;

        // IHumanoidCharacterView Implementation
        public IHumanoidMovementView Movement => _movement;
        public IHumanoidLookView Look => _look;

        public HumanoidInputState InputState
        {
            get => _netInputState.Value;
            set
            {
                if (IsOwner) _netInputState.Value = value;
            }
        }

        public bool IsCapturedBy(ulong playerId) => PossessorId == playerId;

        public bool CanPossess(ulong playerId)
        {
            // If this is a networked player object, only the actual network owner can possess it.
            if (IsSpawned && IsLocalPlayer)
            {
                return OwnerClientId == playerId;
            }

            // Otherwise (bots, vehicles, or before spawn), allow if unowned or matching ID
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
            _netInputState.OnValueChanged += OnInputStateChanged;

            // Server-side: Initialize the shared network state based on the Netcode assigned owner
            if (IsServer)
            {
                _netOwnerId.Value = OwnerClientId;
                _netPossessorId.Value = OwnerClientId;
            }

            // Initial sync for the local client
            if (OwnerId.HasValue) NotifyPossessionChanged(OwnerId.Value, true);
        }

        public override void OnNetworkDespawn()
        {
            _netOwnerId.OnValueChanged -= OnOwnerChanged;
            _netInputState.OnValueChanged -= OnInputStateChanged;
            base.OnNetworkDespawn();
        }

        private void OnOwnerChanged(ulong previous, ulong current)
        {
            if (current != ulong.MaxValue) NotifyPossessionChanged(current, true);
            else NotifyPossessionChanged(previous, false);
        }

        private void OnInputStateChanged(HumanoidInputState previous, HumanoidInputState current)
        {
            if (!IsOwner)
            {
                InputState = current;
            }
        }

        private void NotifyPossessionChanged(ulong playerId, bool possessed)
        {
            // Only notify local components (Camera, Controls) if this is the local player's possession event.
            // This prevents the Host from "possessing" joining clients' characters locally.
            if (NetworkManager.Singleton != null && playerId != NetworkManager.Singleton.LocalClientId)
            {
                return;
            }

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
            // Sync Input State for remote simulation
            _netInputState.Value = InputState;

            var groundTransform = _movement.CurrentGround.GroundTransform;
            if (groundTransform != null)
            {
                var netObj = groundTransform.GetComponentInParent<NetworkObject>();
                if (netObj != null)
                {
                    _netParentPlatform.Value = netObj;
                    _netLocalPosition.Value = netObj.transform.InverseTransformPoint(transform.position);
                    _netRotation.Value = transform.rotation;
                    return;
                }
            }

            // Default: No networked ground under feet
            _netParentPlatform.Value = new NetworkObjectReference();
            _netPosition.Value = transform.position;
            _netRotation.Value = transform.rotation;
        }

        private void UpdateFromNetwork()
        {
            // HARD SYNC / RECONCILIATION:
            // We use the networked position as the authoritative truth.
            // If the local simulation drifts too far, we interpolate/snap to corrected position.

            Vector3 targetWorldPos;
            if (_netParentPlatform.Value.TryGet(out NetworkObject netObj))
            {
                targetWorldPos = netObj.transform.TransformPoint(_netLocalPosition.Value);
            }
            else
            {
                targetWorldPos = _netPosition.Value;
            }

            // Only correct if the drift is significant (e.g. > 10cm)
            float drift = Vector3.Distance(transform.position, targetWorldPos);
            if (drift > 0.1f)
            {
                // Smoothly pull the character towards the correct position
                transform.position = Vector3.Lerp(transform.position, targetWorldPos, Time.deltaTime * 10f);
            }

            // Always smooth rotation towards the authoritative truth
            transform.rotation = Quaternion.Slerp(transform.rotation, _netRotation.Value, Time.deltaTime * 15f);
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
