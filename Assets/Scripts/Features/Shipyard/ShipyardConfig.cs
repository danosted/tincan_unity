#nullable enable
using TinCan.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace TinCan.Features.Shipyard
{
    /// <summary>Shipyard tunables: where the preview stands, the camera, the frame tools, and the assets it draws with.</summary>
    [CreateAssetMenu(fileName = "ShipyardConfig", menuName = "TinCan/Ship/Shipyard Config")]
    public class ShipyardConfig : ScriptableObject
    {
        [Header("Stage")]
        [Tooltip("Where the preview ship stands in the world, away from anything else in the scene.")]
        public Vector3 StageOrigin = new(0f, 200f, 0f);
        [Tooltip("Side of the square build floor drawn under the ship, in metres.")]
        [Min(4f)] public float FloorSize = 40f;

        [Header("Camera (free flying)")]
        [Tooltip("Degrees per unit of mouse movement while looking.")]
        [Min(0f)] public float LookSpeed = 0.15f;
        [Tooltip("Flying speed (m/s).")]
        [Min(0f)] public float FlySpeed = 8f;
        [Tooltip("Flying speed is multiplied by this while Fast is held.")]
        [Min(1f)] public float FastMultiplier = 3f;
        [Tooltip("Metres per scroll-wheel notch, straight along the view (the Input System reports about 1 per notch).")]
        [Min(0f)] public float ZoomStep = 2f;
        [Range(5f, 89f)] public float MaxPitch = 89f;
        [Tooltip("Mouse movement (in Look units) under which a right-button press counts as a click: it deselects the node in frame mode.")]
        [Min(0f)] public float ClickSlop = 6f;
        [Range(20f, 100f)] public float FieldOfView = 60f;
        [Tooltip("A fresh view of the ship: from this far at least (m), turned and tilted this much (degrees).")]
        [Min(1f)] public float StartDistance = 18f;
        public float StartYaw = 35f;
        public float StartPitch = 30f;

        [Header("Frame tools")]
        [Tooltip("Frame units (10 cm) a dragged or placed node snaps to: 5 is half a metre.")]
        [Min(1)] public int FrameSnap = 5;
        [Tooltip("How close (m) the cursor's ray must pass to a node or an edge to pick it.")]
        [Min(0.05f)] public float PickRadius = 0.35f;
        [Tooltip("Pixels the mouse moves up, while Alt-dragging a node, to raise it one frame unit (10 cm).")]
        [Min(1f)] public float HeightPixelsPerUnit = 8f;
        [Tooltip("Pixels the mouse must move, with the button held on a node, before it drags: less is a click (select).")]
        [Min(0f)] public float DragThresholdPixels = 6f;
        [Tooltip("How far (frame units, 10 cm) Extrude pushes a wall or grows a floor edge out.")]
        [Min(1)] public int ExtrudeLength = 20;
        [Tooltip("The height of a new frame's first story, in frame units.")]
        [Min(1)] public int StoryHeight = 30;
        [Tooltip("Node markers: their size (m) and colours.")]
        [Min(0.05f)] public float NodeMarkerSize = 0.3f;
        public Color NodeColour = new(0.95f, 0.85f, 0.4f, 1f);
        public Color SelectedColour = new(0.3f, 0.8f, 1f, 1f);
        public Color HoverColour = new(1f, 1f, 1f, 1f);

        [Header("Assets")]
        [Tooltip("Transparent material for the part about to be placed; tinted by GhostValid and GhostBlocked.")]
        public Material? GhostMaterial;
        public Color GhostValid = new(0.3f, 1f, 0.4f, 0.45f);
        public Color GhostBlocked = new(1f, 0.25f, 0.2f, 0.45f);
        [Tooltip("How much bigger than the part its delete highlight is drawn, so it shows over the part.")]
        [Min(1f)] public float HighlightScale = 1.06f;
        public Material? FloorMaterial;
        [Tooltip("Menu_Shipyard: save, load, new, launch, exit.")]
        public MenuDefinition? Menu;
        [Tooltip("Panel settings for the shipyard's panels (parts, tools, controls).")]
        public PanelSettings? PanelSettings;
    }
}
