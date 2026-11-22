#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// A ship's anchor, a socket fitting: press Interact on it to drop it or raise it. While it is down, the ship it is
    /// on holds still (<see cref="AnchorHoldUseCase"/>). Every peer sees whether it is down.
    /// </summary>
    public interface IAnchor : IActor, IInteractable
    {
        /// <summary>The ship this anchor is on (its parent), if any.</summary>
        Transform? Ship { get; }

        bool IsDown { get; }

        /// <summary>Server only: drop (true) or raise (false) the anchor.</summary>
        void ServerSetDown(bool down);
    }
}
