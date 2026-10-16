#nullable enable
using TinCan.Core.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Infrastructure Layer: the winch on a leash point (the upgrade over a plain point). An Interact target: its
    /// InteractionDefinition (IA_CrankWinch) routes to <see cref="CrankWinchInteractionHandler"/>. Its point is the
    /// <see cref="ILeashPoint"/> on the same fixture.
    /// </summary>
    public class LeashWinchNetworkMediator : NetworkBehaviour, IInteractionTarget, ILeashWinch
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;
        private ILeashPoint? _point;

        public InteractionDefinition Definition => _interactionDefinition!;
        public ILeashPoint? Point => _point ??= GetComponentInParent<ILeashPoint>();
    }
}
