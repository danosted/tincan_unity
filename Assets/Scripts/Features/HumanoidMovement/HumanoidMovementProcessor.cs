using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Pure logic for calculating humanoid movement and gravity.
    /// Decoupled from Unity components.
    /// </summary>
    public class HumanoidMovementProcessor
    {
        public Vector3 ProjectMovementOnGround(Vector3 movementDirection, Vector3 groundNormal)
        {
            if (movementDirection.sqrMagnitude < 0.0001f || groundNormal.sqrMagnitude < 0.0001f)
            {
                return movementDirection;
            }

            Vector3 projectedDirection = Vector3.ProjectOnPlane(movementDirection, groundNormal.normalized);
            if (projectedDirection.sqrMagnitude < 0.0001f)
            {
                return Vector3.zero;
            }

            return projectedDirection.normalized * movementDirection.magnitude;
        }

        public Vector3 CalculateHorizontalVelocity(Vector3 currentVelocity, Vector3 targetDirection, float targetSpeed, float acceleration, float deceleration, float deltaTime)
        {
            Vector3 targetVelocity = targetDirection * targetSpeed;
            float rate = targetDirection.sqrMagnitude > 0.01f ? acceleration : deceleration;
            return Vector3.MoveTowards(currentVelocity, targetVelocity, rate * deltaTime);
        }

        public float CalculateVerticalVelocity(float currentVertical, float gravity, bool isGrounded, bool isPlatformSupported, bool isJumping, float jumpForce, float deltaTime)
        {
            if (isGrounded || isPlatformSupported)
            {
                if (isJumping) return jumpForce;
                if (currentVertical < 0) return -2f; // Ground stickiness
            }

            return currentVertical - (gravity * deltaTime);
        }
    }
}
