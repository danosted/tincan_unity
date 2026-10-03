#nullable enable
using UnityEngine;

namespace TinCan.Features.ThirdPersonCamera
{
    /// <summary>Tunables for the third-person rig: an orbit around the body. Referenced by its installer.</summary>
    [CreateAssetMenu(fileName = "ThirdPersonCameraConfig", menuName = "TinCan/Camera/Third Person Camera Config")]
    public class ThirdPersonCameraConfig : ScriptableObject
    {
        [Tooltip("How far behind the orbit centre the camera sits, in metres.")]
        [Min(0f)] public float Distance = 5f;

        [Tooltip("Height of the orbit centre above the body root (the capsule centre). Targeting's CameraAim looks from here.")]
        public float Height;
    }
}
