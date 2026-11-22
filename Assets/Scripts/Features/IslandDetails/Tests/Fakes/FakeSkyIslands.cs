#nullable enable
using System.Collections.Generic;
using TinCan.Features.SkyIslands;

namespace TinCan.Features.IslandDetails.Tests.Fakes
{
    /// <summary>The islands standing are whatever the test puts in <see cref="List"/>.</summary>
    public sealed class FakeSkyIslands : ISkyIslands
    {
        public List<SkyIslandSpec> List { get; } = new();
        public int Seed => 1;
        public IEnumerable<SkyIslandSpec> Standing => List;
        public int StandingCount => List.Count;
        public int Pending => 0;
    }
}
