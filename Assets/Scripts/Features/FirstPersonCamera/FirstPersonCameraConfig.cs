#nullable enable
using UnityEngine;

namespace TinCan.Features.FirstPersonCamera
{
    /// <summary>Tunables for the first-person rig: the camera at the player's eyes. Referenced by its installer.</summary>
    [CreateAssetMenu(fileName = "FirstPersonCameraConfig", menuName = "TinCan/Camera/First Person Camera Config")]
    public class FirstPersonCameraConfig : ScriptableObject
    {
        [Tooltip("Vertical field of view while looking through the player's eyes, in degrees.")]
        [Range(40f, 110f)] public float FieldOfView = 75f;

        [Tooltip("Near clip plane, in metres; small so nearby rails and held items are not cut.")]
        [Min(0.01f)] public float NearClip = 0.05f;

        [Tooltip("Hide the player's own body from their camera (it still casts its shadow). Held items stay visible.")]
        public bool HideBody = true;

        [Header("Held items")]
        [Tooltip("How quickly held items catch up with the look, per second. Higher is tighter; lower sways more.")]
        [Min(0.1f)] public float HeldSwaySharpness = 16f;

        [Tooltip("The most held items trail the look, in degrees, so a fast turn never swings them out of view.")]
        [Range(0f, 20f)] public float HeldSwayMaxAngle = 5f;

        [Tooltip("Layer held items are drawn on, by an overlay camera on top of the world so they never clip into walls. " +
                 "Must exist in Tags and Layers; empty draws them with the world (they can clip).")]
        public string HeldItemLayer = "FirstPersonHeld";
    }
}
