#nullable enable

namespace TinCan.Features.CloudBoundary
{
    /// <summary>The shape of the cloud surface (<see cref="CloudSurfaceQuery"/>).</summary>
    public enum CloudTopology
    {
        /// <summary>Level at the base altitude.</summary>
        Flat,
        /// <summary>Perlin hills that never move. The drawn sea cannot follow it; use <see cref="Swell"/> for that.</summary>
        Rolling,
        /// <summary>Two octaves of Perlin hills that never move. The drawn sea cannot follow it either.</summary>
        Layered,
        /// <summary>Long, slow waves that roll on the shared clock (<see cref="CloudSeaFormula"/>). The drawn sea follows
        /// the same waves exactly.</summary>
        Swell
    }
}
