#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard: build a ship design, save and load it, and launch a session flying it. Solo and local: nothing here
    /// is networked. The keys and the menu drive it; scenarios and tests call it directly.
    /// </summary>
    public interface IShipyard
    {
        bool IsOpen { get; }

        /// <summary>The design being built (the last one, while closed; null before the first Open).</summary>
        ShipDesign? Design { get; }

        ShipPartDefinition? SelectedPart { get; }
        byte Orientation { get; }

        /// <summary>The height level new parts go on.</summary>
        int Level { get; }

        void ChangeLevel(int delta);

        /// <summary>In delete mode the part under the cursor is highlighted and a click deletes it.</summary>
        bool DeleteMode { get; }

        void ToggleDeleteMode();

        /// <summary>The last thing the shipyard has to say: what was saved, why an edit was refused.</summary>
        string Message { get; }

        IReadOnlyList<ShipDesignListing> SavedDesigns { get; }

        void Open();

        /// <summary>Closes the shipyard (the menus are the caller's: the Exit command returns to the main menu).</summary>
        void Close();

        /// <summary>Shows a line in the shipyard's HUD.</summary>
        void Say(string message);

        bool Select(string partId);
        void Rotate();
        bool PlaceAt(ShipGridCell cell);
        bool RemoveAt(ShipGridCell cell);
        bool Undo();
        bool Redo();

        bool Save(string name);
        bool Load(string name);
        void New();

        /// <summary>
        /// Flies the design: offline, hosts a session whose ship is built from it; as the host, rebuilds the ships in play
        /// from it. Refused while the design cannot fly.
        /// </summary>
        bool Launch();
    }
}
