#nullable enable
namespace TinCan.Features.ShipSockets
{
    /// <summary>
    /// Loadouts by design key: the built-in ones that ship with the game, and the player's beside their saved designs.
    /// A design without a loadout file has an empty loadout, which is not an error.
    /// </summary>
    public interface IShipFittingLoadoutStore
    {
        /// <summary>The design's loadout (empty if it has none); false with the reason if its file cannot be read.</summary>
        bool TryLoad(string designKey, out ShipFittingLoadout loadout, out string? error);

        /// <summary>Writes the design's loadout; a built-in design's loadout cannot be overwritten.</summary>
        bool TrySave(string designKey, ShipFittingLoadout loadout, out string? error);
    }
}
