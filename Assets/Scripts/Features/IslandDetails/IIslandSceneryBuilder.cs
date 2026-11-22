#nullable enable
using System.Collections.Generic;
using TinCan.Features.SkyIslands;

namespace TinCan.Features.IslandDetails
{
    /// <summary>Puts an island's scenery (boulders, bushes) in the scene and takes it out again. A seam over Unity objects.</summary>
    public interface IIslandSceneryBuilder
    {
        /// <summary>Builds the scenery among <paramref name="details"/> (trees are skipped); the handle goes back to <see cref="Remove"/>.</summary>
        object Build(in SkyIslandSpec island, IReadOnlyList<IslandDetail> details);

        void Remove(object built);

        /// <summary>Removes everything this builder made.</summary>
        void Clear();
    }
}
