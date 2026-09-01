using UnityEngine;
using Unity.Netcode;
using TinCan.Features.Interaction;

namespace TinCan.Features.Events
{
    /// <summary>
    /// Presentation Layer: A station that grants a player an ability when interacted with.
    /// Logic is handled by InteractionOrchestrator -> ActivateAbilityInteractionHandler.
    /// </summary>
    public class ToggleShipTagStation : NetworkBehaviour, IInteractionTarget
    {
        [SerializeField] private InteractionDefinition _interactionDefinition;

        public InteractionDefinition Definition => _interactionDefinition;
    }
}
