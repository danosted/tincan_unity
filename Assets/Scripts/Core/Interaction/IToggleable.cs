#nullable enable
using TinCan.Core.Domain;

namespace TinCan.Core.Interaction
{
    /// <summary>
    /// Something Interact flips between two states (a door open or shut, an anchor down or up), through the one
    /// <see cref="ToggleInteractionHandler"/>. The state is the target's own, replicated by it.
    /// </summary>
    public interface IToggleable : IInteractable
    {
        /// <summary>Server: flips it; returns the new state (true: open, down, on).</summary>
        bool ServerToggle();
    }
}
