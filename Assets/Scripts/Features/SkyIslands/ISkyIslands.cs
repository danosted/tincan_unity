#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.SkyIslands
{
    /// <summary>The islands standing on this peer right now. Every peer builds its own from the same layout.</summary>
    public interface ISkyIslands
    {
        /// <summary>The seed the islands come from: the voyage's, or the config's before any voyage.</summary>
        int Seed { get; }

        /// <summary>Islands built and standing.</summary>
        IEnumerable<SkyIslandSpec> Standing { get; }

        int StandingCount { get; }

        /// <summary>Islands wanted but not built yet (they are built a few per frame).</summary>
        int Pending { get; }
    }
}
