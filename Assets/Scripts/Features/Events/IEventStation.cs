using TinCan.Features.Interaction;
using TinCan.Features.Abilities;

namespace TinCan.Features.Events
{
    /// <summary>
    /// Domain Layer: Interface for an interactable station that contributes to the ship's state.
    /// Acts as a data provider for the EventStationUseCase.
    /// </summary>
    public interface IEventStation : IInteractable
    {
        AbilityDefinition InteractionAbility { get; }
    }
}
