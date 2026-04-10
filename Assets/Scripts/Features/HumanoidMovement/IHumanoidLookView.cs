using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Interface for any look behavior (1st person, 3rd person)
    /// associated with a humanoid character.
    /// </summary>
    public interface IHumanoidLookView
    {
        bool IsActive { get; }
        float Pitch { get; set; }
        float Yaw { get; set; }
        float Sensitivity { get; }
        float MaxPitch { get; }

        void ApplyLook(float pitch, float yaw);
    }
}
