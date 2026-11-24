#nullable enable
using UnityEngine;

namespace TinCan.Features.CrewBody
{
    /// <summary>What the body is doing, as the animator shows it. Read by the scenario probe.</summary>
    public enum CrewBodyGait
    {
        Idle,
        Run,
        Sprint,
        Airborne,
    }

    /// <summary>Processor: from the measured movement to the animator's speed and the gait it shows.</summary>
    public static class CrewBodyProcessor
    {
        /// <summary>Slower than this (m/s) the body stands.</summary>
        public const float IdleBelow = 0.5f;

        /// <summary>
        /// The animator's Speed: ground speed in m/s, negative when moving mostly backward (the run plays in reverse).
        /// Sideways movement plays the run forward. <paramref name="bodyLocalVelocity"/> is in the body's own axes.
        /// </summary>
        public static float SignedSpeed(Vector3 bodyLocalVelocity)
        {
            float speed = new Vector2(bodyLocalVelocity.x, bodyLocalVelocity.z).magnitude;
            return bodyLocalVelocity.z < -0.5f * speed ? -speed : speed;
        }

        /// <summary>The gait for a ground speed (unsigned), halfway between run and sprint speed telling them apart.</summary>
        public static CrewBodyGait Gait(bool isGrounded, float speed, float runSpeed, float sprintSpeed)
        {
            if (!isGrounded) return CrewBodyGait.Airborne;
            speed = Mathf.Abs(speed);
            if (speed < IdleBelow) return CrewBodyGait.Idle;
            return speed < 0.5f * (runSpeed + sprintSpeed) ? CrewBodyGait.Run : CrewBodyGait.Sprint;
        }
    }
}
