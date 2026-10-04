#nullable enable
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>A shipyard stage without scene objects: it records what it was shown; the cursor ray is set by the test.</summary>
    public sealed class FakeShipyardStage : IShipyardStage
    {
        public bool IsShown { get; private set; }
        public ShipDesign? Shown { get; private set; }
        public int DesignsShown { get; private set; }
        public ShipyardRay Ray { get; set; } = new(new Vector3(0f, 10f, 0f), Vector3.up);
        public (ShipPartDefinition Part, ShipGridCell Cell, byte Orientation, bool Valid)? Ghost { get; private set; }
        public Vector2 Orbited { get; private set; }
        public float Zoomed { get; private set; }

        public void Show() => IsShown = true;

        public void Hide()
        {
            IsShown = false;
            Ghost = null;
        }

        public void ShowDesign(ShipDesign design)
        {
            Shown = design;
            DesignsShown++;
        }

        public ShipyardRay CursorRay(Vector2 screenPoint) => Ray;

        public void ShowGhost(ShipPartDefinition part, ShipGridCell cell, byte orientation, bool valid) => Ghost = (part, cell, orientation, valid);

        public void HideGhost() => Ghost = null;

        public void Orbit(Vector2 degrees) => Orbited += degrees;

        public void Zoom(float metres) => Zoomed += metres;
    }
}
