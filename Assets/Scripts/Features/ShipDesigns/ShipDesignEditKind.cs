#nullable enable
namespace TinCan.Features.ShipDesigns
{
    public enum ShipDesignEditKind
    {
        /// <summary>A new part, with the design's next instance id.</summary>
        Place,
        Remove,
        /// <summary>Puts a removed part back with its own instance id (undoing a remove).</summary>
        Restore,
    }
}
