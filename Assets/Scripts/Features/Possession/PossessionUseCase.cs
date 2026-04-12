using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using TinCan.Core.Domain.Networking;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// Application Layer: Manages the possession of different IPossessable actors.
    /// Handles switching between characters, vehicles, or cameras using the ActorRegistry.
    /// </summary>
    public class PossessionUseCase : ITickable, IInitializable
    {
        private readonly IInputService _inputService;
        private readonly INetworkService _networkService;
        private readonly IActorRegistry _registry;
        private IPossessable _currentActor;

        public PossessionUseCase(IInputService inputService, INetworkService networkService, IActorRegistry registry)
        {
            _inputService = inputService;
            _networkService = networkService;
            _registry = registry;
        }

        public void Initialize()
        {
            Debug.Log("[PossessionUseCase] Initializing...");
        }

        public void Tick()
        {
            ulong localId = _networkService.LocalClientId;

            // Auto-possess first ALLOWED actor if none possessed yet
            if (_currentActor == null)
            {
                var possessables = _registry.GetActors<IPossessable>()
                    .Where(p => p.CanPossess(localId))
                    .ToList();

                if (possessables.Count > 0)
                {
                    Possess(possessables[0]);
                }
            }

            if (_inputService.WasActionTriggered(ActionNames.ToggleControl))
            {
                SwitchToNext();
            }
        }

        public void SwitchToNext()
        {
            ulong localId = _networkService.LocalClientId;
            var possessables = _registry.GetActors<IPossessable>()
                .Where(p => p.CanPossess(localId))
                .ToList();

            if (possessables.Count <= 1)
            {
                Debug.Log("[PossessionUseCase] No other allowed possessable actors to switch to.");
                return;
            }

            int currentIndex = possessables.IndexOf(_currentActor);
            int nextIndex = (currentIndex + 1) % possessables.Count;

            Possess(possessables[nextIndex]);
        }

        public void Possess(IPossessable target)
        {
            if (target == null) return;

            // Unpossess current
            if (_currentActor != null)
            {
                Debug.Log($"[PossessionUseCase] Unpossessing current actor");
                _currentActor.OnUnpossessed();
            }

            _currentActor = target;

            ulong localId = _networkService.LocalClientId;
            Debug.Log($"[PossessionUseCase] Possessing actor '{target}' for PlayerId: {localId}");
            _currentActor.OnPossessed(localId);

            var mono = _currentActor as MonoBehaviour;
            string targetName = mono != null ? mono.gameObject.name : "Unknown";
            Debug.Log($"[PossessionUseCase] Successfully possessed: {targetName}. OwnerId: {_currentActor.OwnerId}");
        }
    }
}
