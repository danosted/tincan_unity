using UnityEngine;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Domain Layer: Pure logic for calculating airship movement.
    /// Decoupled from Unity components and networking.
    /// </summary>
    public class AirshipMovementProcessor
    {
        public Vector3 CalculateLinearVelocity(
            Vector3 currentVelocity,
            AirshipInputState input,
            Transform transform,
            float maxForward,
            float maxBackward,
            float accel,
            float decel,
            float deltaTime)
        {
            float targetSpeed = input.Throttle > 0
                ? input.Throttle * maxForward
                : input.Throttle * maxBackward;

            // Get current speed relative to forward direction
            float currentSpeed = Vector3.Dot(currentVelocity, transform.forward);

            // Apply acceleration or deceleration
            float rate = input.Throttle != 0 ? accel : decel;
            float speed = Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);

            // Return world-space velocity
            return transform.forward * speed;
        }

        public Vector3 CalculateAngularVelocity(
            AirshipInputState input,
            Transform transform,
            float turnSpeed,
            float pitchSpeed)
        {
            // Calculate local rotations first
            float yawAmount = input.Yaw * turnSpeed * Mathf.Deg2Rad;
            float pitchAmount = input.Pitch * pitchSpeed * Mathf.Deg2Rad;

            // Transform local rotation axes to world space
            Vector3 worldYaw = transform.up * yawAmount;
            Vector3 worldPitch = transform.right * pitchAmount;

            return worldYaw + worldPitch;
        }
    }
}
