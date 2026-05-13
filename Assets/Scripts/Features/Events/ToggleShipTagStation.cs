using UnityEngine;
using Unity.Netcode;
using TinCan.Features.Abilities;

namespace TinCan.Features.Events
{
    /// <summary>
    /// Presentation Layer: A station that grants a player an ability when interacted with.
    /// Logic is handled by EventStationUseCase.
    /// </summary>
    public class ToggleShipTagStation : NetworkBehaviour, IEventStation
    {
        [SerializeField] private AbilityDefinition _interactionAbility;

        public AbilityDefinition InteractionAbility => _interactionAbility;
    }
}
