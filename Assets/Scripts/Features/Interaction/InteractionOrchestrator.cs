using System;
using System.Collections.Generic;
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Application Layer: Orchestrates interaction requests by routing them to specific handlers.
    /// This is where the "handling" logic is decoupled from the Views.
    /// </summary>
    public class InteractionOrchestrator : IInteractionOrchestrator
    {
        private readonly IActorRegistry _actorRegistry;
        private readonly IInteractionTargetResolver _targetResolver;
        private readonly IInteractionHandlerRegistry _handlerRegistry;
        private readonly IVehicleBoardingUseCase _vehicleBoardingUseCase;
        private readonly HashSet<Type> _warnedHandlerTypes = new();

        public InteractionOrchestrator(
            IActorRegistry actorRegistry,
            IInteractionTargetResolver targetResolver,
            IInteractionHandlerRegistry handlerRegistry,
            IVehicleBoardingUseCase vehicleBoardingUseCase)
        {
            _actorRegistry = actorRegistry;
            _targetResolver = targetResolver;
            _handlerRegistry = handlerRegistry;
            _vehicleBoardingUseCase = vehicleBoardingUseCase;
        }

        public void HandleInteraction(InteractionRequest request)
        {
            if (!_actorRegistry.TryGetActor(request.RequesterActorId, out var requester) ||
                !_targetResolver.TryResolve(request.TargetId, out var target) ||
                target is not IInteractionTarget interactionTarget)
            {
                return;
            }

            HandleInteraction(requester, interactionTarget);
        }

        public void HandleInteraction(IActor requester, IInteractionTarget target)
        {
            if (target.Definition == null) return;
            if (!_handlerRegistry.TryGetHandler(target.Definition.HandlerType, out var handler))
            {
                WarnMissingHandler(target.Definition);
                return;
            }

            handler.Handle(new InteractionContext(requester, target, target.Definition));
        }

        // A target whose handler no loaded installer registers would otherwise do nothing, silently. Once per type.
        private void WarnMissingHandler(InteractionDefinition definition)
        {
            var handlerType = definition.HandlerType;
            if (handlerType == null || !_warnedHandlerTypes.Add(handlerType)) return;

            Debug.LogWarning($"[InteractionOrchestrator] No {handlerType.Name} is registered for {definition.name}. " +
                             "Its feature installer is probably not in this scene's FeatureProfile.");
        }

        public void HandleExit()
        {
            Debug.Log($"[InteractionOrchestrator] Routing exit request");
            _vehicleBoardingUseCase.ExitVehicle();
        }
    }
}
