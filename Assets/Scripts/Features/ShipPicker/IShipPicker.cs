#nullable enable
using TinCan.Core.UI;

namespace TinCan.Features.ShipPicker
{
    /// <summary>
    /// Temporary (.docs/plans/ship-picker-temporary.md): the host picks any stored design, built-in or saved, and the ships
    /// in play rebuild as it.
    /// </summary>
    public interface IShipPicker
    {
        /// <summary>The picker menu, built now from the store's list: one row per design, the note (if any), then Back.</summary>
        MenuDefinition BuildMenu(string? note = null);

        /// <summary>Selects the stored design and gives it to every ship in play; false with the reason if refused.</summary>
        bool Pick(string key, out string message);
    }
}
