#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Server: something solid pushes the ship. The ship is kinematic, so physics never stops it; a feature that finds it
    /// inside something (sky islands) pushes it out through here. <see cref="AirshipMovementUseCase"/> moves the ship by
    /// the push at once and takes away the velocity it has into the surface, with a small bounce, so the next tick does
    /// not drive it straight back in. Plan: .docs/plans/sky-islands.md.
    /// </summary>
    public interface IAirshipCollisionResponse
    {
        /// <param name="push">World-space offset that takes the ship out; its direction is the surface's outward normal.</param>
        void Push(IAirshipView airship, Vector3 push);
    }
}
