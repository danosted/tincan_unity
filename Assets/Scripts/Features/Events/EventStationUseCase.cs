using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.Abilities;

namespace TinCan.Features.Events
{
    /// <summary>
    /// Application Layer: Handles the logic of interacting with an event station.
    /// Treating interaction as a toggle for an ability granted to the player.
    /// </summary>
    public class EventStationUseCase
    {
        private readonly AbilitySystemUseCase _abilitySystem;

        public EventStationUseCase(AbilitySystemUseCase abilitySystem)
        {
            _abilitySystem = abilitySystem;
        }

        public void HandleStationInteraction(IActor interactor, IEventStation station)
        {
            if (interactor is not IAbilityControllerBase playerController || station.InteractionAbility == null) return;

            // Resolve the target controller from the station (e.g., the Airship)
            var targetController = (station as MonoBehaviour)?.GetComponentInParent<IAbilityControllerBase>();

            // Toggle logic: If the player already has the tag associated with this ability, remove it.
            // (Note: This assumes AbilityTag is a unique identifier for the station capability)
            if (playerController.HasTag(station.InteractionAbility.AbilityTag))
            {
                Debug.Log($"[EventStationUseCase] Toggling OFF station ability: {station.InteractionAbility.name}");
                playerController.RemoveAbility(station.InteractionAbility);
            }
            else
            {
                Debug.Log($"[EventStationUseCase] Toggling ON station ability: {station.InteractionAbility.name}");
                playerController.GrantAbility(station.InteractionAbility);
                playerController.TryActivateAbility(station.InteractionAbility, targetController);
            }
        }
    }
}
