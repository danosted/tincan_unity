#nullable enable

namespace TinCan.Features.IslandDetails
{
    /// <summary>
    /// What a detail on an island is. Trees (firewood) and veins (fuel crystals) are networked; the rest are scenery on
    /// every peer.
    /// </summary>
    public enum IslandDetailKind
    {
        Tree,
        Boulder,
        Bush,
        Vein
    }
}
