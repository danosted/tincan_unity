using UnityEngine;
using TinCan.Core.Domain;
using TinCan.Features.Possession;
using VContainer;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Infrastructure Layer: Allows a boarded player to interact with an airship control panel to take control.
    /// </summary>
    public class AirshipControlPanel : MonoBehaviour
    {
        private PossessionUseCase _possessionUseCase;
        private IInputService _inputService;
        private IPossessable _possessable;

        private bool _playerInControlZone = false;

        [Inject]
        public void Construct(PossessionUseCase possessionUseCase, IInputService inputService)
        {
            _possessionUseCase = possessionUseCase;
            _inputService = inputService;
        }

        private void Start()
        {
            _possessable = GetComponentInParent<IPossessable>();
            if (_possessable == null)
            {
                Debug.LogError($"IPossessable not found in parent of {gameObject.name}. Disabling control panel.");
                enabled = false;
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInControlZone = true;
                Debug.Log("Player entered control panel zone. Press Interact to take control.");
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Player"))
            {
                _playerInControlZone = false;
            }
        }

        private void Update()
        {
            if (!_playerInControlZone || _possessionUseCase == null) return;

            if (_inputService.WasActionTriggered(ActionNames.Interact))
            {
                _possessionUseCase.Possess(_possessable);
                Debug.Log("Player triggered possession of airship.");
            }
        }
    }
}
