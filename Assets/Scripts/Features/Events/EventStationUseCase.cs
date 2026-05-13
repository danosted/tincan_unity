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
            if (station.InteractionAbility == null) return;

            // Resolve the target controller from the station (e.g., the Airship)
            var targetController = (station as MonoBehaviour)?.GetComponentInParent<IAbilityControllerBase>();
            if (targetController == null) return;

            // In a proper GAS architecture, the Ship should own the ability, not the player.
            targetController.GrantAbility(station.InteractionAbility);

            // Toggle logic natively using GAS
            if (targetController.HasTag(station.InteractionAbility.AbilityTag))
            {
                Debug.Log($"[EventStationUseCase] Toggling OFF station ability on target: {station.InteractionAbility.name}");
                _abilitySystem.CancelAbility(targetController, station.InteractionAbility);
            }
            else
            {
                Debug.Log($"[EventStationUseCase] Toggling ON station ability on target: {station.InteractionAbility.name}");
                _abilitySystem.TryActivateAbility(targetController, station.InteractionAbility, targetController);
            }
        }
    }
}
