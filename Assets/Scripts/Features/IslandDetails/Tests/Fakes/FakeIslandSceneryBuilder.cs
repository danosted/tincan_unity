#nullable enable
using System.Collections.Generic;
using TinCan.Features.IslandDetails;
using TinCan.Features.SkyIslands;

namespace TinCan.Features.IslandDetails.Tests.Fakes
{
    /// <summary>Records what was built (a copy of the details per island) and what was removed.</summary>
    public sealed class FakeIslandSceneryBuilder : IIslandSceneryBuilder
    {
        public Dictionary<SkyIslandId, List<IslandDetail>> Built { get; } = new();
        public List<SkyIslandId> Removed { get; } = new();
        public int Clears { get; private set; }

        public object Build(in SkyIslandSpec island, IReadOnlyList<IslandDetail> details)
        {
            Built[island.Id] = new List<IslandDetail>(details);
            return island.Id;
        }

        public void Remove(object built)
        {
            var id = (SkyIslandId)built;
            Built.Remove(id);
            Removed.Add(id);
        }

        public void Clear()
        {
            Built.Clear();
            Clears++;
        }
    }
}
