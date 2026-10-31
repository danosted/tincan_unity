#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Ship.Sockets
{
    /// <summary>
    /// What is mounted in each ship's sockets, on this peer (from replicated state). Implemented by the ship sockets
    /// feature; read by features that act on fittings (ship damage puts a breakable point beside each) without
    /// referencing it.
    /// </summary>
    public interface IMountedFittings
    {
        /// <summary>The ship's mounted fittings whose socket the ship has built on this peer, in socket order.</summary>
        IReadOnlyList<MountedFittingInfo> FittingsOn(Guid shipId);
    }
}
