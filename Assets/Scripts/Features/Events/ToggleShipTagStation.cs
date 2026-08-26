using UnityEngine;
using Unity.Netcode;
using TinCan.Features.Abilities;
using TinCan.Features.Interaction;

namespace TinCan.Features.Events
{
    /// <summary>
    /// Presentation Layer: A station that grants a player an ability when interacted with.
    /// Logic is handled by EventStationUseCase.
    /// </summary>
    public class ToggleShipTagStation : NetworkBehaviour, IEventStation, IInteractionTarget
    {
        [SerializeField] private InteractionDefinition _interactionDefinition;

        public AbilityDefinition InteractionAbility => _interactionDefinition?.Ability;
        public InteractionDefinition Definition => _interactionDefinition;
    }
}
