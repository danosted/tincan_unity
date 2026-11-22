#nullable enable
using UnityEngine;

namespace TinCan.Features.Anchor
{
    /// <summary>
    /// Pure ladder rules: whether a body is on the ladder, how it moves there, and when it has climbed to the top.
    /// Forward climbs, back goes down, nothing hangs still, and jump lets go. On the ground a body walks past the ladder
    /// unless it pushes forward.
    /// </summary>
    public static class LadderProcessor
    {
        public const float ClimbSpeed = 2.5f;

        /// <summary>How far from the ladder's line a body still takes hold of it.</summary>
        public const float Reach = 0.9f;

        /// <summary>How far below the ladder's foot a body can still reach up to it (from the ground, or a jump).</summary>
        public const float ReachBelowFoot = 1.5f;

        /// <summary>How fast a body on the ladder is drawn onto its line.</summary>
        private const float SnapSpeed = 3f;

        /// <summary>Whether a body (its feet at <paramref name="feet"/>) is within reach of the ladder.</summary>
        public static bool IsOn(Vector3 feet, in AnchorLadder ladder)
        {
            float low = Mathf.Min(ladder.Foot.y, ladder.Top.y) - ReachBelowFoot;
            if (feet.y < low || feet.y > ladder.Top.y) return false;

            Vector3 line = LineAt(feet.y, ladder);
            return Vector3.ProjectOnPlane(feet - line, Vector3.up).magnitude <= Reach;
        }

        /// <summary>
        /// The body's velocity this step when the ladder takes it; false when it does not (out of reach, jumping off, or on
        /// the ground without pushing forward), and the step's own velocity stands.
        /// </summary>
        public static bool TryClimb(Vector3 feet, in AnchorLadder ladder, float forward, bool jumping, bool grounded, out Vector3 velocity)
        {
            velocity = default;
            if (jumping || !IsOn(feet, ladder)) return false;

            bool up = forward > 0.5f;
            bool down = forward < -0.5f;
            if (grounded && !up) return false;

            float climb = up ? ClimbSpeed : down ? -ClimbSpeed : 0f;
            Vector3 toLine = Vector3.ProjectOnPlane(LineAt(feet.y, ladder) - feet, Vector3.up);
            velocity = Vector3.up * climb + Vector3.ClampMagnitude(toLine * SnapSpeed, SnapSpeed);
            return true;
        }

        /// <summary>Whether a body on the ladder has climbed to the top, to be set down on deck.</summary>
        public static bool HasClimbed(Vector3 feet, in AnchorLadder ladder, float margin = 0.2f) =>
            feet.y >= ladder.Top.y - margin && feet.y <= ladder.Top.y + 1f &&
            Vector3.ProjectOnPlane(feet - ladder.Top, Vector3.up).magnitude <= Reach;

        // The ladder's line at a height: the point between top and foot there (the ship may lean a little).
        private static Vector3 LineAt(float height, in AnchorLadder ladder)
        {
            float span = ladder.Top.y - ladder.Foot.y;
            float t = span > 1e-4f ? Mathf.Clamp01((height - ladder.Foot.y) / span) : 1f;
            return Vector3.Lerp(ladder.Foot, ladder.Top, t);
        }
    }
}
