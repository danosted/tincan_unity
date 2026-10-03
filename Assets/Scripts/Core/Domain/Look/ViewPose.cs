#nullable enable
using UnityEngine;

namespace TinCan.Core.Domain.Look
{
    /// <summary>What a camera rig places the camera from: where the body is drawn, its eye height and the look angles.</summary>
    public readonly struct ViewPose
    {
        /// <summary>Where the body is drawn this frame (interpolated between ticks), not the simulated root.</summary>
        public readonly Vector3 VisualPosition;
        /// <summary>Eye height above the root (the capsule centre).</summary>
        public readonly float EyeHeight;
        public readonly float Pitch;
        public readonly float Yaw;

        public ViewPose(Vector3 visualPosition, float eyeHeight, float pitch, float yaw)
        {
            VisualPosition = visualPosition;
            EyeHeight = eyeHeight;
            Pitch = pitch;
            Yaw = yaw;
        }
    }
}
