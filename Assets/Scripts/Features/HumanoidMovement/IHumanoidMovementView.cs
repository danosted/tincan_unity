using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Interface for the physical movement motor.
    /// Purely behavioral, delegated to by a Character Facade or Mediator.
    /// </summary>
    public interface IHumanoidMovementView : IControllable
    {
        Transform Transform { get; }
        GroundData CurrentGround { get; }
        void UpdateGroundData(GroundData data);

        float WalkSpeed { get; }
        float SprintMultiplier { get; }
        float JumpForce { get; }
        float Gravity { get; }

        /// <summary>
        /// The direction the camera is currently looking (used for movement relativity).
        /// </summary>
        Quaternion LookRotation { get; }

        /// <summary>
        /// Raw sensing data from the physical world.
        /// </summary>
        RaycastHit? LastGroundHit { get; }

        void Move(Vector3 motion);

        /// <summary>
        /// Sets the physical rotation of the character body.
        /// </summary>
        void SetRotation(Quaternion rotation);
    }
}
