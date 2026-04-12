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
        private int _currentIndex = -1;

        public PossessionUseCase(IInputService inputService, INetworkService networkService, IActorRegistry registry)
        {
            _inputService = inputService;
            _networkService = networkService;
            _registry = registry;
        }

        public void Initialize()
        {
            Debug.Log("[PossessionUseCase] Initializing...");
            // For testing: attempt to possess the first available actor after a short delay
            // or just ensure we have a valid index if any exist.
        }

        public void Tick()
        {
            ulong localId = _networkService.LocalClientId;

            // Auto-possess first ALLOWED actor if none possessed yet
            if (_currentIndex == -1)
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
            int nextIndex = (_currentIndex + 1) % possessables.Count;
            Possess(possessables[nextIndex]);
        }

        public void Possess(IPossessable target)
        {
            var possessables = _registry.GetActors<IPossessable>().ToList();
            int index = possessables.IndexOf(target);
            if (index == -1)
            {
                Debug.LogWarning($"[PossessionUseCase] Target {target} not found in registry.");
                return;
            }

            // Unpossess current
            if (_currentIndex >= 0 && _currentIndex < possessables.Count)
            {
                Debug.Log($"[PossessionUseCase] Unpossessing actor at index {_currentIndex}");
                possessables[_currentIndex].OnUnpossessed();
            }

            _currentIndex = index;

            ulong localId = _networkService.LocalClientId;
            Debug.Log($"[PossessionUseCase] Possessing actor '{target}' for PlayerId: {localId}");
            possessables[_currentIndex].OnPossessed(localId);

            var mono = possessables[_currentIndex] as MonoBehaviour;
            string targetName = mono != null ? mono.gameObject.name : "Unknown";
            Debug.Log($"[PossessionUseCase] Successfully possessed: {targetName}");
        }
    }
}
