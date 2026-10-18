#nullable enable
using System;
using TinCan.Core.Ship.Parts;
using TinCan.Features.ShipDesigns;
using TinCan.Features.Shipyard;
using UnityEngine;

namespace TinCan.Tests.EditMode.Fakes
{
    /// <summary>
    /// A shipyard stage without scene objects: it records what it was shown; the cursor ray and whether the cursor is over
    /// a panel are set by the test, and panel clicks are raised with <see cref="ClickPart"/> and <see cref="ClickMode"/>.
    /// </summary>
    public sealed class FakeShipyardStage : IShipyardStage
    {
        public bool IsShown { get; private set; }
        public ShipDesign? Shown { get; private set; }
        public int DesignsShown { get; private set; }
        public ShipyardRay Ray { get; set; } = new(new Vector3(0f, 10f, 0f), Vector3.up);
        public (ShipPartDefinition Part, Vector3 Position, Quaternion Rotation, bool Valid)? Ghost { get; private set; }
        public (ShipPartDefinition Part, Vector3 Position)? Highlighted { get; private set; }
        public float FloorY { get; private set; }
        public ShipyardFrameMarks? Marks { get; private set; }
        public ShipyardCameraPose? CameraPose { get; private set; }
        public bool Looking { get; private set; }
        public ShipyardPanelModel? Panel { get; private set; }
        public bool OverPanel { get; set; }

        public event Action<string>? PartPicked;
        public event Action<ShipyardMode>? ModePicked;

        public void Show() => IsShown = true;

        public void Hide()
        {
            IsShown = false;
            Ghost = null;
            Marks = null;
        }

        public void ShowDesign(ShipDesign design)
        {
            Shown = design;
            DesignsShown++;
        }

        public ShipyardRay CursorRay(Vector2 screenPoint) => Ray;

        public void ShowLevel(float floorY) => FloorY = floorY;

        public void ShowGhost(ShipPartDefinition part, Vector3 position, Quaternion rotation, bool valid)
        {
            Highlighted = null;
            Ghost = (part, position, rotation, valid);
        }

        public void ShowHighlight(ShipPartDefinition part, Vector3 position, Quaternion rotation)
        {
            Ghost = null;
            Highlighted = (part, position);
        }

        public void HideGhost()
        {
            Ghost = null;
            Highlighted = null;
        }

        public void ShowFrameMarks(ShipFrame frame, ShipyardFrameMarks marks) => Marks = marks;

        public void HideFrameMarks() => Marks = null;

        public void PlaceCamera(ShipyardCameraPose pose) => CameraPose = pose;

        public void SetLooking(bool looking) => Looking = looking;

        public void ShowPanel(ShipyardPanelModel model) => Panel = model;

        public bool IsPointerOverPanel(Vector2 screenPoint) => OverPanel;

        public void ClickPart(string partId) => PartPicked?.Invoke(partId);

        public void ClickMode(ShipyardMode mode) => ModePicked?.Invoke(mode);
    }
}
