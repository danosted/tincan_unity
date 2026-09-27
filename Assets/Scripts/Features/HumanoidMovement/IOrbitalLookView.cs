using UnityEngine;
using TinCan.Core.Domain;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Interface for look behavior (1st person, 3rd person).
    /// Purely behavioral, delegated to by a Character Facade or Mediator.
    /// </summary>
    public interface IOrbitalLookView
    {
        float Pitch { get; set; }
        float Yaw { get; set; }
        float Sensitivity { get; }
        float MaxPitch { get; }

        /// <summary>Height of the orbit centre above the body root; the camera looks through it (targeting CameraAim).</summary>
        float OrbitHeight { get; }

        Camera Camera { get; }

        void ApplyLook(float pitch, float yaw);

    }
}
