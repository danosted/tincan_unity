using UnityEngine;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// A generic responder that toggles a Camera component when its parent IControllable is possessed.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class PossessionCameraResponder : MonoBehaviour, IPossessionReceiver
    {
        private Camera _camera;

        private void Awake()
        {
            if (_camera == null)
            {
                _camera = GetComponent<Camera>();
            }

            if (_camera == null)
            {
                _camera = GetComponentInChildren<Camera>();
            }
        }

        public void OnPossessed(ulong playerId)
        {
            if (_camera != null) _camera.enabled = true;
        }

        public void OnUnpossessed()
        {
            if (_camera != null) _camera.enabled = false;
        }
    }
}
