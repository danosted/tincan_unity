using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.Abilities;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    public class MaintenanceUseCase : IMaintenanceUseCase
    {
        private readonly AbilitySystemUseCase _abilitySystem;
        private readonly AbilityDefinition _repairAbilityDef;

        public MaintenanceUseCase(AbilitySystemUseCase abilitySystem, AbilityDefinition repairAbilityDef)
        {
            _abilitySystem = abilitySystem;
            _repairAbilityDef = repairAbilityDef;
        }

        public void RepairModule(IActor interactor, IRepairable target)
        {
            if (target == null || interactor == null) return;

            if (interactor is IAbilityControllerBase interactorController && target.Controller != null)
            {
                if (_repairAbilityDef != null)
                {
                    Debug.Log($"[MaintenanceUseCase] Interactor {interactor.Id} activating Repair Ability on {target.GetType().Name}");
                    _abilitySystem.TryActivateAbility(interactorController, _repairAbilityDef, target.Controller);
                }
                else
                {
                    Debug.LogError("[MaintenanceUseCase] Repair Ability Definition is not assigned! Repair failed.");
                }
            }
            else
            {
                Debug.LogWarning("[MaintenanceUseCase] Interactor or Target does not implement IAbilityControllerBase. Interaction ignored.");
            }
        }
    }
}
