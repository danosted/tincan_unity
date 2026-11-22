#nullable enable

namespace TinCan.Features.IslandDetails
{
    /// <summary>What a detail on an island is. Trees are networked (they give firewood); the rest are scenery on every peer.</summary>
    public enum IslandDetailKind
    {
        Tree,
        Boulder,
        Bush
    }
}
