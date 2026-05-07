using UnityEngine;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Infrastructure;
using VContainer;

namespace TinCan.Features.Abilities
{
    /// <summary>
    /// Static bridge to expose Gameplay Ability System functionality to Unity Visual Scripting.
    /// </summary>
    public static class GASVisualScriptingBridge
    {
        /// <summary>
        /// Applies a Gameplay Effect to a target GameObject if it has an Ability Controller.
        /// Returns a result that can be used for branching in Visual Scripting.
        /// </summary>
        public static GameplayEffectResult ApplyEffectToTarget(GameObject target, GameplayEffectDefinition effect)
        {
            if (target == null) return GameplayEffectResult.Failure("Target is null.");

            var controller = target.GetComponentInChildren<IAbilityControllerBase>();
            if (controller == null)
            {
                // Fallback: check the object itself if not in children
                controller = target.GetComponent<IAbilityControllerBase>();
            }

            if (controller == null)
            {
                return GameplayEffectResult.Failure($"Target {target.name} does not have an IAbilityControllerBase.");
            }

            // Resolve the Ability System from the composition root
            var scope = Object.FindAnyObjectByType<ProjectLifetimeScope>();
            if (scope == null || scope.Container == null)
            {
                return GameplayEffectResult.Failure("ProjectLifetimeScope or Container not found in scene.");
            }

            var abilitySystem = scope.Container.Resolve<AbilitySystemUseCase>();
            if (abilitySystem == null)
            {
                return GameplayEffectResult.Failure("AbilitySystemUseCase not registered in VContainer.");
            }

            return abilitySystem.ApplyGameplayEffect(controller, effect);
        }
    }
}
