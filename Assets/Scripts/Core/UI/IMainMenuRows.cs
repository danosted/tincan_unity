#nullable enable
using System.Collections.Generic;

namespace TinCan.Core.UI
{
    /// <summary>
    /// Implemented by a feature's installer to add rows to the main menu (the shipyard's "Shipyard" row). The rows exist
    /// only where the feature is loaded; they go before the menu's Quit row. Their commands are the feature's own
    /// <see cref="IMenuCommand"/>s.
    /// </summary>
    public interface IMainMenuRows
    {
        IEnumerable<MenuItemDefinition> MainMenuRows { get; }
    }
}
