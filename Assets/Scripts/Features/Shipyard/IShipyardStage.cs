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

        void ShowGhost(ShipPartDefinition part, ShipGridCell cell, byte orientation, bool valid);

        void HideGhost();

        void Orbit(Vector2 degrees);

        void Zoom(float metres);
    }
}
