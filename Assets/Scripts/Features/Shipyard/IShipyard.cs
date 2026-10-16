#nullable enable
using System.Collections.Generic;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// The shipyard: build a ship design, save and load it, and launch a session flying it. Solo and local: nothing here
    /// is networked. The keys, the panels and the menu drive it; scenarios and tests call it directly.
    /// </summary>
    public interface IShipyard
    {
        bool IsOpen { get; }

        /// <summary>The design being built (the last one, while closed; null before the first Open).</summary>
        ShipDesign? Design { get; }

        /// <summary>Building with catalog parts or drawing the frame.</summary>
        ShipyardMode Mode { get; }

        void SetMode(ShipyardMode mode);

        ShipPartDefinition? SelectedPart { get; }
        byte Orientation { get; }

        /// <summary>The height level new parts go on.</summary>
        int Level { get; }

        void ChangeLevel(int delta);

        /// <summary>In delete mode what is under the cursor is highlighted and a click deletes it.</summary>
        bool DeleteMode { get; }

        void ToggleDeleteMode();

        /// <summary>Frame mode: the selected node, if any.</summary>
        int? SelectedNode { get; }

        /// <summary>Frame mode: the story new nodes go on.</summary>
        int Story { get; }

        /// <summary>Frame mode: works on the story above or below (up to one above the top, which a first node starts).</summary>
        void ChangeStory(int delta);

        /// <summary>Where the camera is (preview space).</summary>
        ShipyardCameraPose Camera { get; }

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

        /// <summary>Frame mode: a click straight down onto this ship-space point (x and z), as a player's click there would be.</summary>
        bool FrameClickAt(Vector3 point);

        /// <summary>Frame mode: planks or opens the face straight below this ship-space point.</summary>
        bool FrameFillAt(Vector3 point);

        /// <summary>Frame mode: extrudes what is straight below this ship-space point (a wall, an outer floor edge, a floor).</summary>
        bool FrameExtrudeAt(Vector3 point);

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
