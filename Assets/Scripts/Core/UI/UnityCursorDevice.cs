#nullable enable
using UnityEngine;

namespace TinCan.Core.UI
{
    /// <summary>The only code that writes Unity's <see cref="Cursor"/>; <see cref="CursorPolicyUseCase"/> drives it.</summary>
    public sealed class UnityCursorDevice : ICursorDevice
    {
        public void Apply(CursorState mode)
        {
            bool locked = mode == CursorState.Locked;
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}
