#nullable enable
using TinCan.Core.Domain;
using TinCan.Core.Interaction;
using UnityEngine;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// A ship's anchor, a socket fitting: press Interact on it to drop it or raise it (core's ToggleInteractionHandler).
    /// While it is down, the ship it is on holds still (<see cref="AnchorHoldUseCase"/>) and a rope ladder hangs over the
    /// side to climb back aboard (<see cref="LadderClimbUseCase"/>). Every peer sees whether it is down.
    /// </summary>
    public interface IAnchor : IActor, IToggleable
    {
        /// <summary>The ship this anchor is on (its parent), if any.</summary>
        Transform? Ship { get; }

        bool IsDown { get; }

        /// <summary>The rope ladder that hangs over the side while the anchor is down, if this anchor has one.</summary>
        AnchorLadder? Ladder { get; }

        /// <summary>Server only: drop (true) or raise (false) the anchor.</summary>
        void ServerSetDown(bool down);
    }
}
