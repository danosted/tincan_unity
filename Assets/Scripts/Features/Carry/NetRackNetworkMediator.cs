#nullable enable
using TinCan.Core.Domain;
using TinCan.Features.Interaction;
using Unity.Netcode;
using UnityEngine;

namespace TinCan.Features.Carry
{
    /// <summary>Marker for the deck fixture where the net hangs; keeps the handler testable without a NetworkBehaviour.</summary>
    public interface INetRack : IInteractable { }

    /// <summary>
    /// Presentation/Infrastructure Layer: the net rack on deck. Interacting routes to TakeNetInteractionHandler
    /// via IA_TakeNet. Holds no state: the net itself is the player's carry state.
    /// </summary>
    public class NetRackNetworkMediator : NetworkBehaviour, IInteractionTarget, INetRack
    {
        [SerializeField] private InteractionDefinition? _interactionDefinition;

        public InteractionDefinition Definition => _interactionDefinition!;
    }
}
