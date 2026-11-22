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
    /// down is replicated; its children "Chain" and "Ladder" show while it is down, on every peer. The ladder is placed by
    /// its markers "LadderTop", "LadderFoot" (the line a climber follows) and "LadderDeck" (where a climber lands).
    /// </summary>
    public class AnchorNetworkMediator : NetworkBehaviour, IInteractionTarget, IAnchor, IInteractionPrompt
    {
        private const string ChainName = "Chain";
        private const string LadderName = "Ladder";
        private const string TopName = "LadderTop";
        private const string FootName = "LadderFoot";
        private const string DeckName = "LadderDeck";

        [SerializeField] private InteractionDefinition? _interactionDefinition;

        private readonly NetworkVariable<bool> _down = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private ActorIdentity? _identity;
        private Transform? _chain;
        private Transform? _ladder;
        private Transform? _top;
        private Transform? _foot;
        private Transform? _deck;

        public InteractionDefinition Definition => _interactionDefinition!;
        public Guid Id => (_identity ??= new ActorIdentity(this)).Id;
        public bool IsSimulating => IsSpawned;
        public bool IsDown => _down.Value;

        public string Prompt => IsDown ? "Raise anchor" : "Drop anchor";

        public Transform? Ship
        {
            get
            {
                var ship = transform.parent != null ? transform.parent.GetComponentInParent<IAirshipView>() : null;
                return ship?.Transform;
            }
        }

        public AnchorLadder? Ladder
        {
            get
            {
                _top ??= transform.FindDescendant(TopName);
                _foot ??= transform.FindDescendant(FootName);
                _deck ??= transform.FindDescendant(DeckName);
                if (_top == null || _foot == null || _deck == null) return null;
                return new AnchorLadder(_top.position, _foot.position, _deck.position, _deck.rotation);
            }
        }

        public void ServerSetDown(bool down)
        {
            if (IsServer) _down.Value = down;
        }

        public override void OnNetworkSpawn()
        {
            _down.OnValueChanged += OnDownChanged;
            Show(_down.Value);
        }

        public override void OnNetworkDespawn() => _down.OnValueChanged -= OnDownChanged;

        private void OnDownChanged(bool _, bool down) => Show(down);

        private void Show(bool down)
        {
            _chain ??= transform.FindDescendant(ChainName);
            _ladder ??= transform.FindDescendant(LadderName);
            if (_chain != null) _chain.gameObject.SetActive(down);
            if (_ladder != null) _ladder.gameObject.SetActive(down);
        }
    }
}
