#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain.Input;

namespace TinCan.Core.Input
{
    /// <summary>Turns the device side of actions on and off: only the live contexts' actions listen.</summary>
    public interface IInputActionSwitch
    {
        void SetEnabled(IReadOnlyCollection<InputActionId> enabled);
    }
}
