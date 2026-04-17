using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using TinCan.Features.Possession;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Application Layer: Coordinates input and interaction logic.
    /// Handles both world interaction (raycasting) and vehicle exit logic.
    /// </summary>
    public class InteractivityUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly INetworkService _networkService;
        private readonly IInteractorRegistry _interactionRegistry;
        private readonly IActorRegistry _actorRegistry;
        private readonly IInteractionOrchestrator _orchestrator;

        public InteractivityUseCase(
            IInputService inputService,
            INetworkService networkService,
            IInteractorRegistry interactionRegistry,
            IActorRegistry actorRegistry,
            IInteractionOrchestrator orchestrator)
        {
            _inputService = inputService;
            _networkService = networkService;
            _interactionRegistry = interactionRegistry;
            _actorRegistry = actorRegistry;
            _orchestrator = orchestrator;
        }

        public void Tick()
        {
            if (!_inputService.WasActionTriggered(ActionNames.Interact)) return;


            // 2. Otherwise, check for world interactions
            HandleWorldInteraction();
        }

        private void HandleWorldInteraction()
        {
            ulong localId = _networkService.LocalClientId;
            // Find all interactors (typically just one for the local player)
            foreach (var interactor in _interactionRegistry.AllInteractors)
            {
                if (interactor.Owner is IPossessable possessable && !possessable.IsCapturedBy(localId))
                {
                    continue; // Skip if this interactor's owner is not captured by the local player
                }

                if (interactor.CurrentTarget == null) continue;

                // Route interaction through the orchestrator locally
                Debug.Log($"[InteractivityUseCase] Triggering interaction with {interactor.CurrentTarget.GetType().Name}");
                _orchestrator.HandleInteraction(interactor.CurrentTarget);
            }
        }
    }
}
