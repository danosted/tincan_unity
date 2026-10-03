using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Core.Domain.Look
{
    /// <summary>
    /// Domain Layer: where an actor looks (yaw and pitch, which become its predicted look input) and the camera the
    /// local player sees it through. Where that camera sits is not decided here: a camera rig places it
    /// (<see cref="IViewRig"/>, first or third person, chosen by the feature profile).
    /// </summary>
    public interface ILookView
    {
        float Pitch { get; set; }
        float Yaw { get; set; }
        float Sensitivity { get; }
        float MaxPitch { get; }

        /// <summary>Height above the body root that the camera looks from (targeting CameraAim); the rig decides it.</summary>
        float AimHeight { get; }

        Camera Camera { get; }

        void ApplyLook(float pitch, float yaw);

    }
}
