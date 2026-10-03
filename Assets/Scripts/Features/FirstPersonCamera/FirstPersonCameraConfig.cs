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
    }
}
