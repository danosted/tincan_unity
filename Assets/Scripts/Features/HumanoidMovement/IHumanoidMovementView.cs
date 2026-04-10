using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Interface for the Humanoid view to allow the Application layer
    /// to interact with the physical motor without a direct dependency on CharacterController.
    /// </summary>
    public interface IHumanoidMovementView
    {
        Transform Transform { get; }
        bool IsActive { get; }
        bool IsGrounded { get; }
        float WalkSpeed { get; }
        float SprintMultiplier { get; }
        float JumpForce { get; }
        float Gravity { get; }
        
        void Move(Vector3 motion);
    }
}
