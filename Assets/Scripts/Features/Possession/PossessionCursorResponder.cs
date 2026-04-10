using UnityEngine;

namespace TinCan.Features.Possession
{
    /// <summary>
    /// A responder that handles cursor locking when its parent IControllable is possessed.
    /// </summary>
    public class PossessionCursorResponder : MonoBehaviour, IPossessionResponder
    {
        [SerializeField] private CursorLockMode _lockMode = CursorLockMode.Locked;
        [SerializeField] private bool _hideCursor = true;

        public void OnPossessed()
        {
            Cursor.lockState = _lockMode;
            Cursor.visible = !_hideCursor;
        }

        public void OnUnpossessed()
        {
            // Optional: You might want to unlock cursor when unpossessed,
            // but usually another responder will take over or the UseCase handles the toggle.
        }
    }
}
