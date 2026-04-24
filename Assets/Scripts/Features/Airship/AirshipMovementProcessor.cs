using UnityEngine;

namespace TinCan.Features.Airship
{
    /// <summary>
    /// Domain Layer: Pure logic for calculating airship movement.
    /// Decoupled from Unity components and networking.
    /// </summary>
    public class AirshipMovementProcessor
    {
        public float CalculateLinearSpeed(
            float currentSpeed,
            AirshipInputState input,
            float maxForward,
            float maxBackward,
            float accel,
            float decel,
            float deltaTime)
        {
            float targetSpeed = input.Throttle > 0
                ? input.Throttle * maxForward
                : input.Throttle * maxBackward;

            // Apply acceleration or deceleration
            float rate = input.Throttle != 0 ? accel : decel;
            return Mathf.MoveTowards(currentSpeed, targetSpeed, rate * deltaTime);
        }

        public Vector3 CalculateVelocityWithDrift(
            Vector3 currentVelocity,
            Vector3 targetForwardDirection,
            float currentSpeed,
            float blendRate,
            float deltaTime)
        {
            Vector3 targetVelocity = targetForwardDirection * currentSpeed;
            return Vector3.Lerp(currentVelocity, targetVelocity, blendRate * deltaTime);
        }

        public Vector3 CalculateAngularVelocity(
            Vector3 currentAngularVelocity,
            AirshipInputState input,
            float currentSpeed,
            float maxForwardSpeed,
            float currentRoll,
            float turnSpeed,
            float pitchSpeed,
            float angularAccel,
            float angularDecel,
            float maxBankAngle,
            float bankSpeed,
            float deltaTime)
        {
            // Calculate local rotations (Degrees per second) for Pitch and Yaw
            float targetYawAmount = input.Yaw * turnSpeed;
            float targetPitchAmount = input.Pitch * pitchSpeed;

            // Apply angular momentum (acceleration / deceleration)
            float yawRate = input.Yaw != 0 ? angularAccel : angularDecel;
            float currentYaw = Mathf.MoveTowards(currentAngularVelocity.y, targetYawAmount, yawRate * deltaTime);

            float pitchRate = input.Pitch != 0 ? angularAccel : angularDecel;
            float currentPitch = Mathf.MoveTowards(currentAngularVelocity.x, targetPitchAmount, pitchRate * deltaTime);

            // Calculate Visual Banking (Roll)
            // Bank angle scales with input yaw and current speed
            float speedFactor = Mathf.Clamp01(Mathf.Abs(currentSpeed) / maxForwardSpeed);
            float targetBankAngle = -input.Yaw * maxBankAngle * speedFactor;

            // Convert currentRoll to -180 to 180 range
            if (currentRoll > 180f) currentRoll -= 360f;

            // Smoothly interpolate towards target bank angle
            float rollDifference = targetBankAngle - currentRoll;
            // The angular velocity needed to close the difference this frame
            float currentRollVel = rollDifference * bankSpeed;

            return new Vector3(currentPitch, currentYaw, currentRollVel);
        }
    }
}
