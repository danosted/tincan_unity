#nullable enable
using UnityEngine;

namespace TinCan.Features.GrappleHook
{
    /// <summary>
    /// Pure maths of the grappling hook: where it flies from a look, and one reel step of the holder's velocity toward the
    /// hook point. No state; the same numbers on owner and server.
    /// </summary>
    public sealed class GrappleHookProcessor
    {
        /// <summary>The look's direction: <paramref name="facing"/> is its world yaw, <paramref name="pitch"/> degrees, positive looks down.</summary>
        public static Vector3 AimDirection(Quaternion facing, float pitch) => facing * Quaternion.Euler(pitch, 0f, 0f) * Vector3.forward;

        /// <summary>Whether a surface <paramref name="distance"/> metres away (null: none in sight) is within the hook's reach.</summary>
        public static GrappleAimReach Reach(float? distance, float maxRange) => distance switch
        {
            null => GrappleAimReach.None,
            { } metres when metres <= maxRange => GrappleAimReach.InRange,
            _ => GrappleAimReach.OutOfRange
        };

        /// <summary>
        /// One step of reeling from <paramref name="position"/> toward <paramref name="hookPoint"/>: the velocity turns
        /// toward the reel at <see cref="GrappleHookConfig.ReelAcceleration"/>. Within
        /// <see cref="GrappleHookConfig.ArriveDistance"/> it has arrived: the speed drops to
        /// <see cref="GrappleHookConfig.ArriveSpeed"/> and it hops up at least <see cref="GrappleHookConfig.ArriveHopSpeed"/>.
        /// Beyond <see cref="GrappleHookConfig.BreakDistance"/> the line snaps and the velocity is left alone. When
        /// <paramref name="blocked"/> (something stopped the last step, see <see cref="IsBlocked"/>) it climbs: it rises at
        /// least at <see cref="GrappleHookConfig.ClimbSpeed"/>, up a wall or a rail until it is over the edge.
        /// </summary>
        public (Vector3 Velocity, GrappleReelOutcome Outcome) Reel(Vector3 velocity, Vector3 position, Vector3 hookPoint,
            GrappleHookConfig config, float deltaTime, bool blocked = false)
        {
            Vector3 toHook = hookPoint - position;
            float distance = toHook.magnitude;

            if (distance > config.BreakDistance) return (velocity, GrappleReelOutcome.Snapped);
            if (distance <= config.ArriveDistance) return (Arrival(velocity, config), GrappleReelOutcome.Arrived);

            Vector3 reel = toHook / distance * config.ReelSpeed;
            Vector3 reeled = Vector3.MoveTowards(velocity, reel, config.ReelAcceleration * deltaTime);
            if (blocked) reeled.y = Mathf.Max(reeled.y, config.ClimbSpeed);
            return (reeled, GrappleReelOutcome.Reeling);
        }

        /// <summary>
        /// Whether the last step was stopped short: moving with <paramref name="momentum"/> it should have come
        /// noticeably closer to <paramref name="hookPoint"/>, but the distance (<paramref name="previousDistance"/> then,
        /// measured from <paramref name="position"/> now) shrank by less than <see cref="GrappleHookConfig.StallFraction"/>
        /// of that.
        /// </summary>
        public static bool IsBlocked(Vector3 momentum, Vector3 position, Vector3 hookPoint, float previousDistance,
            GrappleHookConfig config, float deltaTime)
        {
            Vector3 toHook = hookPoint - position;
            float distance = toHook.magnitude;
            if (distance <= Mathf.Epsilon) return false;

            float expected = Vector3.Dot(momentum, toHook / distance) * deltaTime;
            if (expected < MinExpectedProgress) return false;
            return previousDistance - distance < expected * config.StallFraction;
        }

        // Below this much expected progress in a step (metres) the reel is still starting, not blocked.
        private const float MinExpectedProgress = 0.05f;

        private static Vector3 Arrival(Vector3 velocity, GrappleHookConfig config)
        {
            Vector3 kept = Vector3.ClampMagnitude(velocity, config.ArriveSpeed);
            kept.y = Mathf.Max(kept.y, config.ArriveHopSpeed);
            return kept;
        }
    }
}
