using UnityEngine;
using TinCan.Features.Interaction;
using TinCan.Features.Possession;
using Unity.Netcode;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Infrastructure Layer: Allows a boarded player to interact with an airship control panel to take control.
    /// Implements IVehicleBoardable so players can point and interact with it.
    /// </summary>
    public class AirshipControlPanel : NetworkBehaviour, IVehicleBoardable, IInteractionTarget
    {
        [SerializeField] private InteractionDefinition _interactionDefinition;

        private IPossessable _possessableAirship;

        public IPossessable TargetVehicle => _possessableAirship;
        public InteractionDefinition Definition => _interactionDefinition;

        private void Awake()
        {
            _possessableAirship = GetComponentInParent<IPossessable>();
            if (_possessableAirship == null)
            {
                Debug.LogError($"IPossessable not found in parent of {gameObject.name}. Control panel won't function.");
            }
        }
    }
}
