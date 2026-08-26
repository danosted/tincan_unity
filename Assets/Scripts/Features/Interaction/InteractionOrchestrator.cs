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
                target is not IInteractionTarget interactionTarget ||
                interactionTarget.Definition == null ||
                !_handlerRegistry.TryGetHandler(
                    interactionTarget.Definition.HandlerTag,
                    out var handler))
            {
                return;
            }

            handler.Handle(new InteractionContext(
                requester,
                target,
                interactionTarget.Definition));
        }

        public void HandleExit()
        {
            Debug.Log($"[InteractionOrchestrator] Routing exit request");
            _vehicleBoardingUseCase.ExitVehicle();
        }
    }
}
