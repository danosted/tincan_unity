#nullable enable
using UnityEngine;

namespace TinCan.Core.Humanoid
{
    /// <summary>
    /// Processor: how a humanoid looks like it moves, measured on where it is drawn rather than simulated, so every peer
    /// (owner, server, proxies) reads the same facts with nothing replicated. Velocity is taken in the frame of the
    /// ground underfoot (a ship), so a player standing on a moving ship is not running. Plan: crew-animation.md (S1).
    /// </summary>
    public static class HumanoidPresentationProcessor
    {
        /// <summary>Rising faster than this (m/s) is airborne even with ground just below: the start of a jump.</summary>
        public const float RiseSpeed = 1.5f;

        /// <summary>Seconds without ground below that still count as grounded: a step down, the lip of a stair.</summary>
        public const float GroundGraceSeconds = 0.12f;

        /// <summary>The velocity between two samples of the drawn position, both in the same frame (ground or world).</summary>
        public static Vector3 Velocity(Vector3 previousInFrame, Vector3 currentInFrame, float deltaTime) =>
            deltaTime > 0f ? (currentInFrame - previousInFrame) / deltaTime : Vector3.zero;

        /// <summary>Eases <paramref name="current"/> toward <paramref name="target"/>, frame-rate independent.</summary>
        public static Vector3 Smooth(Vector3 current, Vector3 target, float deltaTime, float smoothSeconds) =>
            smoothSeconds <= 0f ? target : Vector3.Lerp(current, target, 1f - Mathf.Exp(-deltaTime / smoothSeconds));

        /// <summary>
        /// Grounded when there is ground just below and the body is not rising off it, or it left the ground only
        /// <see cref="GroundGraceSeconds"/> ago without rising.
        /// </summary>
        public static bool IsGrounded(bool groundBelow, float verticalSpeed, float secondsWithoutGround) =>
            verticalSpeed < RiseSpeed && (groundBelow || secondsWithoutGround < GroundGraceSeconds);
    }
}
