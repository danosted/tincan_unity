using Unity.Netcode;
using UnityEngine;
using TinCan.Features.HumanoidMovement;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using TinCan.Features.Interaction;
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
    [RequireComponent(typeof(InteractorControllerView))]
    [RequireComponent(typeof(NetworkTransformMediator))]
    public class HumanoidPlayer : NetworkMediator, IHumanoidCharacterView, IInteractionMediator
    {
        private HumanoidControllerView _movement;
        private ThirdPersonLookView _look;
        private NetworkTransformMediator _transformSync;

        private readonly NetworkVariable<HumanoidInputState> _netInputState = new NetworkVariable<HumanoidInputState>(
            writePerm: NetworkVariableWritePermission.Owner);

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

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            _movement = GetComponent<HumanoidControllerView>();
            _look = GetComponent<ThirdPersonLookView>();
            _transformSync = GetComponent<NetworkTransformMediator>();

            _netInputState.OnValueChanged += OnInputStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            _netInputState.OnValueChanged -= OnInputStateChanged;
            base.OnNetworkDespawn();
        }

        private void OnInputStateChanged(HumanoidInputState previous, HumanoidInputState current)
        {
            if (!IsOwner)
            {
                InputState = current;
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
            {
                // Sync Input State for remote simulation
                _netInputState.Value = InputState;

                // Update platform for transform sync
                _transformSync.SetPlatform(_movement.CurrentGround.GroundTransform);
            }
        }

        public void RequestInteract(NetworkObject target)
        {
            if (IsOwner)
            {
                RequestInteractServerRpc(target);
            }
        }

        [Rpc(SendTo.Server)]
        private void RequestInteractServerRpc(NetworkObjectReference targetRef)
        {
            if (targetRef.TryGet(out NetworkObject target))
            {
                InteractionOrchestrator?.HandleInteraction(OwnerClientId, target);
            }
        }
    }
}
