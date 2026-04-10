using UnityEngine;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Domain Layer: Pure logic for calculating camera rotation.
    /// Handles pitch and yaw logic independently of Unity's Transform.
    /// </summary>
    public class FreeCameraRotationProcessor
    {
        public struct RotationResult
        {
            public float NewPitch;
            public float NewYaw;
        }

        public RotationResult CalculateRotation(
            float currentPitch,
            float currentYaw,
            Vector2 mouseDelta,
            float sensitivity,
            float maxPitch)
        {
            float yawChange = mouseDelta.x * sensitivity;
            float pitchChange = mouseDelta.y * sensitivity;

            float newYaw = currentYaw + yawChange;
            float newPitch = currentPitch - pitchChange;

            // Clamp pitch to prevent flipping
            newPitch = Mathf.Clamp(newPitch, -maxPitch, maxPitch);

            return new RotationResult
            {
                NewPitch = newPitch,
                NewYaw = newYaw
            };
        }
    }
}
