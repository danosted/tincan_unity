using TinCan.Core.Domain;
using TinCan.Core.Domain.Abilities;
using TinCan.Core.Domain.Abilities.Tags;
using TinCan.Features.Abilities;
using TinCan.Features.Possession;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    public class PossessionInteractionHandler : IInteractionHandler
    {
        private readonly IPossessionAuthority _possessionAuthority;
        private readonly GameplayTag _handlerTag;

        public PossessionInteractionHandler(
            IPossessionAuthority possessionAuthority,
            GameplayTag handlerTag)
        {
            _possessionAuthority = possessionAuthority;
            _handlerTag = handlerTag;
        }

        public GameplayTag Tag => _handlerTag;

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

    public class ToggleAbilityInteractionHandler : IInteractionHandler
    {
        private readonly AbilitySystemUseCase _abilitySystem;
        private readonly GameplayTag _handlerTag;

        public ToggleAbilityInteractionHandler(
            AbilitySystemUseCase abilitySystem,
            GameplayTag handlerTag)
        {
            _abilitySystem = abilitySystem;
            _handlerTag = handlerTag;
        }

        public GameplayTag Tag => _handlerTag;

        public void Handle(InteractionContext context)
        {
            if (context.Definition.Ability == null ||
                context.Target is not MonoBehaviour targetMono ||
                targetMono.GetComponentInParent<IAbilityControllerBase>() is not { } targetController)
            {
                return;
            }

            _abilitySystem.GrantAbility(targetController, context.Definition.Ability);
            if (targetController.HasTag(context.Definition.Ability.AbilityTag))
            {
                _abilitySystem.CancelAbility(targetController, context.Definition.Ability);
                return;
            }

            _abilitySystem.TryActivateAbility(targetController, context.Definition.Ability, targetController);
        }
    }

    public class RepairAbilityInteractionHandler : IInteractionHandler
    {
        private readonly AbilitySystemUseCase _abilitySystem;
        private readonly GameplayTag _handlerTag;

        public RepairAbilityInteractionHandler(
            AbilitySystemUseCase abilitySystem,
            GameplayTag handlerTag)
        {
            _abilitySystem = abilitySystem;
            _handlerTag = handlerTag;
        }

        public GameplayTag Tag => _handlerTag;

        public void Handle(InteractionContext context)
        {
            if (context.Definition.Ability == null ||
                context.Requester is not IAbilityControllerBase requesterController ||
                context.Target is not IRepairable repairable ||
                repairable.Controller == null)
            {
                return;
            }

            _abilitySystem.GrantAbility(requesterController, context.Definition.Ability);
            _abilitySystem.TryActivateAbility(
                requesterController,
                context.Definition.Ability,
                repairable.Controller);
        }
    }
}
