#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Domain.Input
{
    /// <summary>
    /// The player's key bindings, for the Controls menu: what can be changed, what each is bound to, and interactive
    /// rebinding (press the new key; Escape cancels). A key already used by an action that can be live at the same time
    /// is refused (actions whose contexts exclude each other, such as walking and steering the ship, may share keys).
    /// Changes are saved and loaded at start.
    /// </summary>
    public interface IInputBindings
    {
        IReadOnlyList<InputBindingSlot> Slots { get; }

        /// <summary>The key as the player reads it: "Space", "W", "Left Button".</summary>
        string Describe(InputBindingSlot slot);

        /// <summary>The slot waiting for a key, if any.</summary>
        InputBindingSlot? Rebinding { get; }

        /// <summary>Why the last rebind did not stick (a conflict), until the next one starts.</summary>
        string? LastMessage { get; }

        /// <summary>Waits for the next key or button for this slot (every action is silent meanwhile).</summary>
        void StartRebind(InputBindingSlot slot);

        void CancelRebind();

        /// <summary>Back to the defaults in the actions asset.</summary>
        void ResetAll();

        /// <summary>Raised when a binding, the rebinding state or the message changes.</summary>
        event Action? Changed;
    }
}
