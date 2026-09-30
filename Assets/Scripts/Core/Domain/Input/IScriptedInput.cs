#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// Automation seam for bots and scenarios: presses actions the way a player would. Scripted presses obey the same
    /// contexts as devices (an action outside every active context stays released).
    /// </summary>
    public interface IScriptedInput
    {
        /// <summary>Holds a button down until <see cref="Release(InputActionId)"/>.</summary>
        void Press(InputActionId action);

        /// <summary>Holds a value on an axis or vector action (a move direction); several held values add up.</summary>
        void Press(InputActionId action, Vector2 value);

        /// <summary>Releases every held value of the action.</summary>
        void Release(InputActionId action);

        /// <summary>Releases one held value.</summary>
        void Release(InputActionId action, Vector2 value);

        /// <summary>Registers a one-shot press; it stays visible until the end of the first frame in which it is read.</summary>
        void Tap(InputActionId action);

        void Clear();
    }
}
