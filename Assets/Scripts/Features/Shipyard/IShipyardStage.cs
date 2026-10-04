#nullable enable
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>
    /// What the shipyard draws: the preview ship, the ghost of the part about to be placed, and the camera orbiting them.
    /// Exists between <see cref="Show"/> and <see cref="Hide"/>.
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

        /// <summary>Draws the working level's floor (a see-through grid) at that height.</summary>
        void ShowLevel(int level);

        void ShowGhost(ShipPartDefinition part, ShipGridCell cell, byte orientation, bool valid);

        /// <summary>Marks a placed part for deletion: drawn over it in the blocked colour. Hidden by <see cref="HideGhost"/>.</summary>
        void ShowHighlight(ShipPartDefinition part, ShipPartPlacement placement);

        void HideGhost();

        void Orbit(Vector2 degrees);

        void Zoom(float metres);
    }
}
