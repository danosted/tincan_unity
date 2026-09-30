#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// Reads actions for the systems that own a context (movement, look, stations). An action that no active, unblocked
    /// context turns on reads as released and zero, so a reader never checks menus or possession itself. Scripted input
    /// (bots, scenarios) is merged in. Discrete actions with a meaning of their own (Cancel, switch possession) are not
    /// read here: contexts route them to <see cref="IInputCommandHandler"/>s.
    /// </summary>
    public interface IInputReader
    {
        bool IsPressed(InputActionId? action);
        bool WasPressedThisFrame(InputActionId? action);
        float ReadAxis(InputActionId? action);
        Vector2 ReadVector2(InputActionId? action);

        /// <summary>The pressed ability inputs as bits (<c>GameplayInput.BitIndex</c>), for the predicted input state.</summary>
        ulong GameplayInputMask();
    }
}
