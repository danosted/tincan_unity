using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using TinCan.Features.Possession;
using System.Linq;

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
        private readonly PossessionUseCase _possessionUseCase;

        public InteractivityUseCase(
            IInputService inputService,
            INetworkService networkService,
            IInteractorRegistry interactionRegistry,
            PossessionUseCase possessionUseCase,
            IInteractionOrchestrator orchestrator)
        {
            _inputService = inputService;
            _networkService = networkService;
            _interactionRegistry = interactionRegistry;
            _possessionUseCase = possessionUseCase;
            _orchestrator = orchestrator;
        }

        public void Tick()
        {
            if (!_inputService.WasActionTriggered(ActionNames.Interact)) return;

            HandleWorldInteraction();
        }

        private void HandleWorldInteraction()
        {
            if (_possessionUseCase.CurrentPossession is not MonoBehaviour mono) return;
            var interactor = mono.GetComponent<IInteractorView>();
            if (interactor == null || interactor.CurrentTarget == null) return;
            _orchestrator.HandleInteraction(interactor.CurrentTarget);
        }
    }
}
