using VContainer;
using VContainer.Unity;
using TinCan.Core.Domain;
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
        private readonly PossessionUseCase _possessionUseCase;
        private readonly bool _serverTargeted;

        public InteractivityUseCase(
            IInputService inputService,
            PossessionUseCase possessionUseCase)
        {
            _inputService = inputService;
            _possessionUseCase = possessionUseCase;
        }

        // When InteractionFeatureInstaller is active, Interact is a predicted input bit and the server acquires the target
        // itself (InteractInputUseCase). This legacy path, which sends a client-chosen target, then stays silent.
        [Inject]
        public InteractivityUseCase(IInputService inputService, PossessionUseCase possessionUseCase, IObjectResolver resolver)
            : this(inputService, possessionUseCase)
        {
            _serverTargeted = resolver.TryResolve<InteractionTargetingSettings>(out _);
        }

        public void Tick()
        {
            if (_serverTargeted || !_inputService.WasActionTriggered(ActionNames.Interact)) return;

            HandleWorldInteraction();
        }

        private void HandleWorldInteraction()
        {
            if (_possessionUseCase.CurrentPossession is not MonoBehaviour mono) return;
            var interactor = mono.GetComponent<IInteractorView>();
            if (interactor == null || interactor.CurrentTarget == null) return;

            if (mono.TryGetComponent(out IInteractionRequester requester))
            {
                requester.RequestInteraction(interactor.CurrentTarget);
            }
        }
    }
}
