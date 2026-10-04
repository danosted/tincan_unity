#nullable enable
using TinCan.Core.UI;
using UnityEngine;

namespace TinCan.Features.Shipyard
{
    /// <summary>Shipyard tunables: where the preview stands, the camera, and the assets it draws with.</summary>
    [CreateAssetMenu(fileName = "ShipyardConfig", menuName = "TinCan/Ship/Shipyard Config")]
    public class ShipyardConfig : ScriptableObject
    {
        [Header("Stage")]
        [Tooltip("Where the preview ship stands in the world, away from anything else in the scene.")]
        public Vector3 StageOrigin = new(0f, 200f, 0f);
        [Tooltip("Side of the square build floor drawn under the ship, in metres.")]
        [Min(4f)] public float FloorSize = 40f;

        [Header("Camera")]
        [Min(1f)] public float StartDistance = 18f;
        public Vector2 DistanceRange = new(4f, 60f);
        [Tooltip("Degrees per unit of mouse movement while orbiting.")]
        [Min(0f)] public float OrbitSpeed = 0.25f;
        [Tooltip("Metres per scroll-wheel notch (the Input System reports about 1 per notch).")]
        [Min(0f)] public float ZoomStep = 2f;
        [Range(5f, 89f)] public float MaxPitch = 80f;

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
    }
}
