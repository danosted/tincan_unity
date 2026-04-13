using UnityEngine;
using TinCan.Features.Interaction;
using TinCan.Features.Possession;
using VContainer;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Infrastructure Layer: Allows a boarded player to interact with an airship control panel to take control.
    /// Implements IInteractable so players can point and interact with it.
    /// </summary>
    public class AirshipControlPanel : MonoBehaviour, IInteractable
    {
        private PossessionUseCase _possessionUseCase;
        private IPossessable _possessableAirship;

        [Inject]
        public void Construct(PossessionUseCase possessionUseCase)
        {
            _possessionUseCase = possessionUseCase;
        }

        private void Start()
        {
            _possessableAirship = GetComponentInParent<IPossessable>();
            if (_possessableAirship == null)
            {
                Debug.LogError($"IPossessable not found in parent of {gameObject.name}. Control panel won't function.");
            }
        }

        public void OnInteract(ulong interactorId)
        {
            if (_possessableAirship == null || _possessionUseCase == null) return;

            Debug.Log($"[AirshipControlPanel] Triggering possession for player {interactorId}");
            _possessionUseCase.Possess(_possessableAirship);
        }
    }
}
