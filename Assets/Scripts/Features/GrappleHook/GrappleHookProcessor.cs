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
        /// Beyond <see cref="GrappleHookConfig.BreakDistance"/> the line snaps and the velocity is left alone.
        /// </summary>
        public (Vector3 Velocity, GrappleReelOutcome Outcome) Reel(Vector3 velocity, Vector3 position, Vector3 hookPoint,
            GrappleHookConfig config, float deltaTime)
        {
            Vector3 toHook = hookPoint - position;
            float distance = toHook.magnitude;

            if (distance > config.BreakDistance) return (velocity, GrappleReelOutcome.Snapped);
            if (distance <= config.ArriveDistance) return (Arrival(velocity, config), GrappleReelOutcome.Arrived);

            Vector3 reel = toHook / distance * config.ReelSpeed;
            return (Vector3.MoveTowards(velocity, reel, config.ReelAcceleration * deltaTime), GrappleReelOutcome.Reeling);
        }

        private static Vector3 Arrival(Vector3 velocity, GrappleHookConfig config)
        {
            Vector3 kept = Vector3.ClampMagnitude(velocity, config.ArriveSpeed);
            kept.y = Mathf.Max(kept.y, config.ArriveHopSpeed);
            return kept;
        }
    }
}
