using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using TinCan.Features.Possession;

namespace TinCan.Features.Interaction
{
    /// <summary>
    /// Application Layer: Coordinates input and interaction logic.
    /// Checks for the Interact action and triggers OnInteract on the focused object.
    /// </summary>
    public class InteractivityUseCase : ITickable
    {
        private readonly IInputService _inputService;
        private readonly INetworkService _networkService;
        private readonly IInteractorRegistry _interactionRegistry;

        public InteractivityUseCase(
            IInputService inputService,
            INetworkService networkService,
            IInteractorRegistry interactionRegistry)
        {
            _inputService = inputService;
            _networkService = networkService;
            _interactionRegistry = interactionRegistry;
        }

        public void Tick()
        {
            if (!_inputService.WasActionTriggered(ActionNames.Interact)) return;

            ulong localId = _networkService.LocalClientId;

            // Find all interactors (typically just one for the local player)
            foreach (var interactor in _interactionRegistry.AllInteractors)
            {
                if (interactor.Owner is IPossessable possessable && !possessable.IsCapturedBy(localId))
                {
                    continue; // Skip if this interactor's owner is not captured by the local player
                }

                if (interactor.CurrentTarget == null) continue;

                Debug.Log($"[InteractivityUseCase] Player {localId} interacting with {interactor.CurrentTarget.GetType().Name}");
                interactor.CurrentTarget.OnInteract(localId);
            }
        }
    }
}
