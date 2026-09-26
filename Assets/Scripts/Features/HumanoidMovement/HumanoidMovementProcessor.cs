using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Pure logic for calculating humanoid movement and gravity.
    /// Decoupled from Unity components.
    /// </summary>
    public class HumanoidMovementProcessor
    {
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
                // Only launch from rest or while settling: just after take-off the probe can still touch the deck,
                // and a held jump must not reset the rise.
                if (isJumping && currentVertical <= 0f) return jumpForce;
                if (currentVertical < 0) return -2f; // Ground stickiness
            }

            return currentVertical - (gravity * deltaTime);
        }
    }
}
