using UnityEngine;
using TinCan.Features.Interaction;
using TinCan.Features.Possession;
using VContainer;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Infrastructure Layer: Allows a boarded player to interact with an airship control panel to take control.
    /// Implements IVehicleBoardable so players can point and interact with it.
    /// </summary>
    public class AirshipControlPanel : MonoBehaviour, IVehicleBoardable
    {
        private IPossessable _possessableAirship;

        public IPossessable TargetVehicle => _possessableAirship;

        private void Start()
        {
            _possessableAirship = GetComponentInParent<IPossessable>();
            if (_possessableAirship == null)
            {
                Debug.LogError($"IPossessable not found in parent of {gameObject.name}. Control panel won't function.");
            }
        }
    }
}
