#nullable enable
using UnityEngine;

namespace TinCan.DevTools.Scenarios
{
    /// <summary>
    /// Where scenario commands stand a player. A teleport must land the root (the capsule centre, not the feet) at
    /// standing height on the deck. Keeping the current height is not enough: on the server a client's body may still be
    /// falling or reconciling when the command runs.
    /// </summary>
    public static class ScenarioPlacement
    {
        private const float ProbeAbove = 3f;
        private const float ProbeDepth = 8f;

        /// <summary>
        /// Drops <paramref name="stand"/> onto the first solid surface below it (along -<paramref name="up"/>, skipping
        /// triggers and the body's own colliders) and lifts it by the body's root-to-feet offset. Returns the point
        /// unchanged if nothing is found.
        /// </summary>
        public static Vector3 OnGround(Transform body, Vector3 stand, Vector3 up)
        {
            Vector3 from = stand + up * ProbeAbove;
            var hits = Physics.RaycastAll(from, -up, ProbeAbove + ProbeDepth, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            foreach (var hit in hits)
            {
                if (hit.collider.transform.IsChildOf(body) || hit.distance >= nearest) continue;
                nearest = hit.distance;
            }

            if (float.IsPositiveInfinity(nearest)) return stand;
            return from - up * nearest + up * RootAboveFeet(body);
        }

        private static float RootAboveFeet(Transform body)
        {
            var controller = body.GetComponent<CharacterController>();
            if (controller == null) return 1f;
            return (controller.height * 0.5f - controller.center.y) * body.lossyScale.y + controller.skinWidth;
        }
    }
}
