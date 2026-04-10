using System.Collections.Generic;
using System.Linq;
using TinCan.Core.Domain;
using UnityEngine;
using VContainer.Unity;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// Application Layer: Manages the possession of different IControllable actors.
    /// Handles switching between characters, vehicles, or cameras.
    /// </summary>
    public class PossessionUseCase : ITickable, IInitializable
    {
        private readonly IInputService _inputService;
        private readonly List<IControllable> _controllables;
        private int _currentIndex = -1;

        public PossessionUseCase(IInputService inputService, IEnumerable<IControllable> controllables)
        {
            _inputService = inputService;
            // Filter out nulls and duplicates (VContainer might inject the same instance via multiple interfaces)
            _controllables = controllables.Where(c => c != null).Distinct().ToList();
        }

        public void Initialize()
        {
            if (_controllables.Count == 0)
            {
                Debug.LogWarning("[PossessionUseCase] No IControllable actors found in the registry.");
                return;
            }

            // Initially disable all and enable the first one
            foreach (var controllable in _controllables)
            {
                controllable.DisableControls();
            }

            Possess(0);
        }

        public void Tick()
        {
            if (_inputService.WasActionTriggered(ActionNames.ToggleControl))
            {
                SwitchToNext();
            }
        }

        public void SwitchToNext()
        {
            if (_controllables.Count <= 1) return;

            int nextIndex = (_currentIndex + 1) % _controllables.Count;
            Possess(nextIndex);
        }

        public void Possess(int index)
        {
            if (index < 0 || index >= _controllables.Count) return;

            // Disable current
            if (_currentIndex >= 0 && _currentIndex < _controllables.Count)
            {
                _controllables[_currentIndex].DisableControls();
            }

            _currentIndex = index;
            _controllables[_currentIndex].EnableControls();

            var mono = _controllables[_currentIndex] as MonoBehaviour;
            string targetName = mono != null ? mono.gameObject.name : "Unknown";
            Debug.Log($"[PossessionUseCase] Possessed: {targetName}");
        }

        public void Possess(IControllable target)
        {
            int index = _controllables.IndexOf(target);
            if (index != -1)
            {
                Possess(index);
            }
            else
            {
                // If it's a new dynamic controllable, add it
                _controllables.Add(target);
                Possess(_controllables.Count - 1);
            }
        }
    }
}
