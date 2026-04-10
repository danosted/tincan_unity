using UnityEngine;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Interface for the Free Camera view to allow the Application layer
    /// to interact with Unity's Transform without a direct dependency on MonoBehaviour.
    /// </summary>
    public interface IFreeCameraView
    {
        Transform CameraTransform { get; }
        bool IsActive { get; }
        float MoveSpeed { get; }
        float RotationSensitivity { get; }
        float MaxPitchAngle { get; }
        bool ShouldLockCursor { get; }

        float CurrentPitch { get; set; }
        float CurrentYaw { get; set; }

        void ApplyRotation(float pitch, float yaw);
    }
}
