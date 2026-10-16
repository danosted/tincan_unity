#nullable enable
using System;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// What the shipyard draws: the preview ship, the ghost of the part about to be placed, the frame overlay, the panels
    /// (parts, controls) and the free-flying camera. Exists between <see cref="Show"/> and <see cref="Hide"/>.
    /// </summary>
    public interface IShipyardStage
    {
        bool IsShown { get; }

        void Show();

        void Hide();

        /// <summary>Brings the preview in step with the design (only what changed is rebuilt).</summary>
        void ShowDesign(ShipDesign design);

        /// <summary>The cursor's ray in preview space, and the built part it hits.</summary>
        ShipyardRay CursorRay(Vector2 screenPoint);

        /// <summary>Draws the working level's floor (a see-through grid) at that height (metres, preview space).</summary>
        void ShowLevel(float floorY);

        void ShowGhost(ShipPartDefinition part, ShipGridCell cell, byte orientation, bool valid);

        /// <summary>Marks a placed part for deletion: drawn over it in the blocked colour. Hidden by <see cref="HideGhost"/>.</summary>
        void ShowHighlight(ShipPartDefinition part, ShipPartPlacement placement);

        void HideGhost();

        /// <summary>Frame mode: node markers, the selected node, what the cursor is on, the pending line. Hidden by <see cref="HideFrameMarks"/>.</summary>
        void ShowFrameMarks(ShipFrame frame, ShipyardFrameMarks marks);

        void HideFrameMarks();

        /// <summary>Puts the camera there (preview space).</summary>
        void PlaceCamera(ShipyardCameraPose pose);

        /// <summary>While looking around the cursor is hidden and held in place.</summary>
        void SetLooking(bool looking);

        void ShowPanel(ShipyardPanelModel model);

        /// <summary>Whether the cursor is over a panel (a click there is the panel's, not the build's).</summary>
        bool IsPointerOverPanel(Vector2 screenPoint);

        /// <summary>A part tile was clicked in the building panel.</summary>
        event Action<string>? PartPicked;

        /// <summary>A mode tab was clicked.</summary>
        event Action<ShipyardMode>? ModePicked;
    }
}
