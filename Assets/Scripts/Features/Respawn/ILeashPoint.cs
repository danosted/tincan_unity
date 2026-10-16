#nullable enable
using TinCan.Core.Domain;
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>
    /// Where a ship's leash is tied: the rope's anchor (outside the rail) and the spot on deck a climbed-up player stands
    /// on. A ship fixture, so it moves with the ship and every peer sees the same one.
    /// </summary>
    public interface ILeashPoint : IActor
    {
        Transform Anchor { get; }
        Transform Stand { get; }

        /// <summary>The ship this point is on (its parent), if any.</summary>
        Transform? Ship { get; }
    }
}
