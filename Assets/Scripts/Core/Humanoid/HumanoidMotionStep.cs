#nullable enable
using UnityEngine;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// One simulated movement step as an <see cref="IHumanoidMotionContributor"/> sees it. <see cref="Velocity"/> is the
    /// body's world velocity in m/s, with its own momentum, jump and gravity already applied; what a contributor leaves
    /// there is what the body moves by this step and the momentum it keeps. <see cref="Momentum"/> is the velocity the
    /// body carried into the step, before its own acceleration, braking and gravity: a contributor that takes the body
    /// over entirely (a reeling hook) starts from it, so the legs' braking does not fight the pull.
    /// </summary>
    public struct HumanoidMotionStep
    {
        public Vector3 Velocity;
        public readonly Vector3 Momentum;
        public readonly float DeltaTime;
        public readonly bool IsGrounded;

        /// <summary>The owner is re-simulating an input it already predicted once: reproduce the motion, change no state.</summary>
        public readonly bool IsReplay;

        public HumanoidMotionStep(Vector3 velocity, Vector3 momentum, float deltaTime, bool isGrounded, bool isReplay)
        {
            Velocity = velocity;
            Momentum = momentum;
            DeltaTime = deltaTime;
            IsGrounded = isGrounded;
            IsReplay = isReplay;
        }
    }
}
