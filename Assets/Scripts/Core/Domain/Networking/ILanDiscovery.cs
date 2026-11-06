#nullable enable
using System;
using System.Collections.Generic;

namespace TinCan.Core.Domain.Networking
{
    /// <summary>
    /// Games on this network, found by a broadcast that every host and dedicated server answers while it runs. Browse
    /// only while a list is on screen. .docs/plans/hosting-and-joining.md (S2).
    /// </summary>
    public interface ILanDiscovery
    {
        /// <summary>The games that answered recently, in the order they were found. Empty while not browsing.</summary>
        IReadOnlyList<DiscoveredGame> Games { get; }

        bool Browsing { get; }

        /// <summary>Raised when a game appears, changes (players) or goes away.</summary>
        event Action? Changed;

        /// <summary>Starts or stops looking. Stopping forgets the list.</summary>
        void Browse(bool browsing);
    }
}
