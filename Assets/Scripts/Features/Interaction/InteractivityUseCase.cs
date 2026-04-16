using VContainer.Unity;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using TinCan.Features.Possession;
using Unity.Netcode;

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

        public InteractivityUseCase(
            IInputService inputService,
            INetworkService networkService,
            IInteractorRegistry interactionRegistry,
            IActorRegistry actorRegistry)
        {
            _inputService = inputService;
            _networkService = networkService;
            _interactionRegistry = interactionRegistry;
            _actorRegistry = actorRegistry;
        }

        public void Tick()
        {
            if (!_inputService.WasActionTriggered(ActionNames.Interact)) return;

            ulong localId = _networkService.LocalClientId;

            // 1. Check if we are currently in a vehicle and want to exit
            if (HandleVehicleExit(localId)) return;

            // 2. Otherwise, check for world interactions
            HandleWorldInteraction(localId);
        }

        private bool HandleVehicleExit(ulong localId)
        {
            // If the local player is currently possessing something that supports exiting (e.g. a Vehicle Mediator),
            // pressing Interact should request an exit.
            foreach (var actor in _actorRegistry.AllActors)
            {
                if (actor is IPossessable possessable && possessable.PossessorId == localId)
                {
                    if (actor is IExitVehicleMediator exitMediator)
                    {
                        Debug.Log($"[InteractivityUseCase] Player {localId} requesting exit from {actor.GetType().Name}");
                        exitMediator.RequestExitVehicle();
                        return true;
                    }
                }
            }
            return false;
        }

        private void HandleWorldInteraction(ulong localId)
        {
            // Find all interactors (typically just one for the local player)
            foreach (var interactor in _interactionRegistry.AllInteractors)
            {
                if (interactor.Owner is IPossessable possessable && !possessable.IsCapturedBy(localId))
                {
                    continue; // Skip if this interactor's owner is not captured by the local player
                }

                if (interactor.CurrentTarget == null) continue;

                // Request interaction via the mediator
                if (interactor.Owner is IInteractionMediator mediator)
                {
                    // Find the network object for the target
                    var targetObj = (interactor.CurrentTarget as MonoBehaviour)?.GetComponentInParent<NetworkObject>();
                    if (targetObj != null)
                    {
                        Debug.Log($"[InteractivityUseCase] Player {localId} requesting interaction with {targetObj.name}");
                        mediator.RequestInteract(targetObj);
                    }
                }
            }
        }
    }
}
