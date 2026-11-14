#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>
    /// A hole at a moment on the shared clock: how open it is (easing open, then easing shut), where it has drifted to,
    /// and its shape for <see cref="CloudSeaFormula"/>. Pure; every peer gets the same answer for the same time.
    /// </summary>
    public class CloudHoleProcessor
    {
        /// <summary>0 before it opens and once it has closed, 1 while fully open.</summary>
        public float Openness(in CloudHole hole, float time)
        {
            float opening = Ease(time - hole.OpenedAt, hole.OpenSeconds);
            if (hole.ClosesAt >= CloudHole.Never) return opening;

            return opening * (1f - Ease(time - hole.ClosesAt, hole.CloseSeconds));
        }

        public Vector2 CentreAt(in CloudHole hole, float time) =>
            hole.Centre + hole.Drift * Mathf.Max(time - hole.OpenedAt, 0f);

        /// <summary>True once the hole has shut for good, so it can be dropped.</summary>
        public bool HasClosed(in CloudHole hole, float time) =>
            hole.ClosesAt < CloudHole.Never && time >= hole.ClosesAt + hole.CloseSeconds;

        /// <summary>The hole's shape at <paramref name="time"/>: its open part grows with its openness, and its rim is
        /// <paramref name="rimFraction"/> of its full radius (at least a metre).</summary>
        public CloudHoleShape Shape(in CloudHole hole, float time, float rimFraction)
        {
            float openness = Openness(hole, time);
            float rim = Mathf.Max(hole.Radius * rimFraction, 1f);
            return new CloudHoleShape(CentreAt(hole, time), hole.Radius * openness, rim, openness);
        }

        private static float Ease(float elapsed, float seconds)
        {
            if (seconds <= 0f) return elapsed >= 0f ? 1f : 0f;

            return CloudSeaFormula.SmoothStep(0f, seconds, elapsed);
        }
    }
}
