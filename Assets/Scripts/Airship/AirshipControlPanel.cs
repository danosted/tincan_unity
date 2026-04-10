// using UnityEngine;
// using TinCan.Core.Domain;
// using TinCan.Features.Possession;
// using VContainer;

// /// <summary>
// /// Allows a boarded player to interact with an airship control panel (e.g., to possess the airship).
// /// </summary>
// public class AirshipControlPanel : MonoBehaviour
// {
//     private PossessionUseCase _possessionUseCase;
//     private IInputService _inputService;
//     private AirshipController _airshipController;

//     private bool _playerInControlZone = false;

//     [Inject]
//     public void Construct(PossessionUseCase possessionUseCase, IInputService inputService)
//     {
//         _possessionUseCase = possessionUseCase;
//         _inputService = inputService;
//     }

//     private void Start()
//     {
//         _airshipController = GetComponentInParent<AirshipController>();
//         if (_airshipController == null)
//         {
//             Debug.LogError($"AirshipController not found in parent of {gameObject.name}. Disabling control panel.");
//             enabled = false;
//         }
//     }

//     private void OnTriggerEnter(Collider other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             _playerInControlZone = true;
//             Debug.Log("Player entered control panel zone. Press Interact to take control.");
//         }
//     }

//     private void OnTriggerExit(Collider other)
//     {
//         if (other.CompareTag("Player"))
//         {
//             _playerInControlZone = false;
//         }
//     }

//     private void Update()
//     {
//         if (!_playerInControlZone || _possessionUseCase == null) return;

//         if (_inputService.WasActionTriggered(ActionNames.Interact))
//         {
//             _possessionUseCase.Possess(_airshipController);
//             Debug.Log("Player possessed airship.");
//         }
//     }
// }
