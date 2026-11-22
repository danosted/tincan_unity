#nullable enable
using System;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Entities;
using TinCan.Core.Interaction;
using TinCan.Core.Ship;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// Infrastructure Layer: the anchor fitting, a networked ship part (with an EntityNetworkMediator). An Interact target:
    /// its InteractionDefinition (IA_ToggleAnchor) routes to <see cref="ToggleAnchorInteractionHandler"/>. Whether it is
    /// down is replicated; its child "Chain" shows while it is down, on every peer.
    /// </summary>
    public class AnchorNetworkMediator : NetworkBehaviour, IInteractionTarget, IAnchor
    {
        private const string ChainName = "Chain";

        [SerializeField] private InteractionDefinition? _interactionDefinition;

        private readonly NetworkVariable<bool> _down = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private ActorIdentity? _identity;
        private Transform? _chain;

        public InteractionDefinition Definition => _interactionDefinition!;
        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;
        public bool IsDown => _down.Value;

        public Transform? Ship
        {
            get
            {
                var ship = transform.parent != null ? transform.parent.GetComponentInParent<IAirshipView>() : null;
                return ship?.Transform;
            }
        }

        public void ServerSetDown(bool down)
        {
            if (IsServer) _down.Value = down;
        }

        public override void OnNetworkSpawn()
        {
            _down.OnValueChanged += OnDownChanged;
            ShowChain(_down.Value);
        }

        public override void OnNetworkDespawn() => _down.OnValueChanged -= OnDownChanged;

        private void OnDownChanged(bool _, bool down) => ShowChain(down);

        private void ShowChain(bool down)
        {
            _chain ??= transform.FindDescendant(ChainName);
            if (_chain != null) _chain.gameObject.SetActive(down);
        }
    }
}
