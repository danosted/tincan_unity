#nullable enable
using System.Collections.Generic;

namespace TinCan.Features.ShipDesigns
{
    /// <summary>
    /// Saved designs (the player's, as files) and the built-in ones that ship with the game. Built-in names are
    /// reserved: a built-in always loads by its name, and no design is saved under one.
    /// </summary>
    public interface IShipDesignStore
    {
        IReadOnlyList<ShipDesignListing> List();

        ShipDesignDecodeResult Load(string key);

        bool TrySave(string key, ShipDesign design, out string? error);

        /// <summary>Deletes a saved design (never a built-in one); false if there was none.</summary>
        bool TryDelete(string key);
    }
}
