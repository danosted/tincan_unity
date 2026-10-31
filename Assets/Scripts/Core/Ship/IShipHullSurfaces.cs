#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// The planked surfaces of each ship built from a design (floors and walls of its frame), on this peer, in ship-local
    /// space. Implemented by whatever builds ships from designs; read by features that put things on the hull (ship
    /// damage points) without referencing how ships are built. <see cref="Version"/> changes whenever a ship's surfaces do.
    /// </summary>
    public interface IShipHullSurfaces
    {
        int Version { get; }

        IReadOnlyList<ShipHullSurface> SurfacesOf(Guid shipId);
    }
}
