using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Features.Abilities;
using TinCan.Features.Possession;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    public class PossessionInteractionHandler : IInteractionHandler
    {
        private readonly IPossessionAuthority _possessionAuthority;

        public PossessionInteractionHandler(IPossessionAuthority possessionAuthority)
        {
            _possessionAuthority = possessionAuthority;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Target is IVehicleBoardable boardable)
            {
                _possessionAuthority.TryAcquirePossession(
                    context.Requester.Id,
                    boardable.TargetVehicle);
            }
        }
    }

    /// <summary>
    /// Activates (or toggles) the definition's Ability, resolving actor/target per ActorRole.
    /// Does not grant the ability — the actor must already have it (starting abilities, equipment, skill tree, etc.).
    /// </summary>
    public class ActivateAbilityInteractionHandler : IInteractionHandler
    {
        private readonly AbilitySystemUseCase _abilitySystem;

        public ActivateAbilityInteractionHandler(AbilitySystemUseCase abilitySystem)
        {
            _abilitySystem = abilitySystem;
        }

        public void Handle(InteractionContext context)
        {
            if (context.Definition.Ability == null ||
                !TryResolveTargetController(context.Target, out var targetController))
            {
                return;
            }

            // One arm per ActorRole; add a case here if a new role is ever needed.
            var (abilityController, target) = context.Definition.AbilityActivator switch
            {
                InteractionDefinition.AbilityActivatorType.Requester when context.Requester is IAbilityControllerBase requesterController
                    => (requesterController, targetController),
                InteractionDefinition.AbilityActivatorType.Target
                    => (targetController, targetController),
                _ => (null, null)
            };

            if (abilityController == null) return;

            _abilitySystem.TryActivateAbility(abilityController, context.Definition.Ability, target);
            // TODO: publish event on ability activation outcome
        }

        // IRepairable exposes its controller directly; other targets resolve one via the component hierarchy.
        private static bool TryResolveTargetController(IInteractable target, out IAbilityControllerBase controller)
        {
            if (target is IRepairable repairable)
            {
                controller = repairable.Controller;
                return controller != null;
            }

            controller = (target as MonoBehaviour)?.GetComponentInParent<IAbilityControllerBase>();
            return controller != null;
        }
    }
}
