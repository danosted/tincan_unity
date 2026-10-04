#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyIslands
{
    /// <summary>Puts islands in the scene and takes them out again. A seam over Unity objects, so streaming stays testable.</summary>
    public interface ISkyIslandBuilder
    {
        /// <summary>Builds the island; the returned handle goes back to <see cref="Remove"/>.</summary>
        object Build(in SkyIslandSpec island);

        void Remove(object built);

        /// <summary>Does any island standing now (streamed or not) come within <paramref name="reach"/> of the point?</summary>
        bool AnyNear(Vector3 point, float reach);

        /// <summary>Removes every island this builder made, and whatever holds them.</summary>
        void Clear();
    }
}
