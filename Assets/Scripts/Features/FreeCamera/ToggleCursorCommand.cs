#nullable enable
using TinCan.Core.Domain.Input;
using UnityEngine;

namespace TinCan.Features.FreeCamera
{
    /// <summary>Free or recapture the cursor while flying the free camera. Handled by <see cref="ToggleCursorInputHandler"/>.</summary>
    [CreateAssetMenu(fileName = "Command_ToggleCursor", menuName = "TinCan/Input/Commands/Toggle Cursor")]
    public sealed class ToggleCursorCommand : InputCommand { }
}
