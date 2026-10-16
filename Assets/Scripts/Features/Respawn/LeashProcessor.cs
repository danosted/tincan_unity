#nullable enable
using UnityEngine;

namespace TinCan.Features.Respawn
{
    /// <summary>Pure rope maths: how much the rope shortens in a step, and the velocity that keeps a body on it.</summary>
    public sealed class LeashProcessor
    {
        /// <summary>The rope after one step: climbing and a reeling winch each shorten it, never below zero.</summary>
        public static float Shorten(float length, bool climbing, bool winching, RespawnRules rules, float deltaTime)
        {
            float rate = (climbing && rules.CanClimb ? rules.ClimbSpeed : 0f) + (winching ? rules.WinchSpeed : 0f);
            return Mathf.Max(0f, length - rate * deltaTime);
        }

        /// <summary>
        /// The velocity for one step that keeps the body within <paramref name="length"/> of <paramref name="anchor"/>:
        /// unchanged while the rope is slack; otherwise the move is pulled back onto the rope's reach (which drags the
        /// body behind a moving anchor), no faster than <paramref name="maxSpeed"/>.
        /// </summary>
        public static Vector3 Constrain(Vector3 position, Vector3 velocity, Vector3 anchor, float length, float maxSpeed, float deltaTime)
        {
            if (deltaTime <= 0f) return velocity;

            Vector3 next = position + velocity * deltaTime;
            Vector3 fromAnchor = next - anchor;
            if (fromAnchor.magnitude <= length) return velocity;

            Vector3 held = anchor + fromAnchor.normalized * length;
            return Vector3.ClampMagnitude((held - position) / deltaTime, maxSpeed);
        }

        /// <summary>Whether a body at <paramref name="position"/> has reached the leash point (or the rope is all in).</summary>
        public static bool HasArrived(Vector3 position, Vector3 anchor, float length, float arriveDistance) =>
            length <= arriveDistance || Vector3.Distance(position, anchor) <= arriveDistance;
    }
}
