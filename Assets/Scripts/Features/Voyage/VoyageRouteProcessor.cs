#nullable enable
using UnityEngine;

namespace TinCan.Features.Voyage
{
    /// <summary>
    /// Domain, pure: the route of a voyage. Distances are measured level (the ship's altitude does not count), so
    /// climbing or diving neither helps nor hurts progress.
    /// </summary>
    public class VoyageRouteProcessor
    {
        /// <summary>The destination: <paramref name="length"/> metres along the ship's level heading, at its altitude.</summary>
        public Vector3 Destination(Vector3 shipPosition, Quaternion shipRotation, float length) =>
            shipPosition + LevelForward(shipRotation) * length;

        /// <summary>Metres left to fly, level.</summary>
        public float DistanceLeft(Vector3 shipPosition, Vector3 destination) => Level(destination - shipPosition).magnitude;

        /// <summary>0 at the start, 1 at the destination. Flying away from it only lowers progress back toward 0.</summary>
        public float Progress(Vector3 shipPosition, Vector3 destination, float length) =>
            length <= 0f ? 1f : Mathf.Clamp01(1f - DistanceLeft(shipPosition, destination) / length);

        public bool HasArrived(Vector3 shipPosition, Vector3 destination, float radius) =>
            DistanceLeft(shipPosition, destination) <= radius;

        /// <summary>Degrees the destination lies off the ship's level heading: positive to starboard (right), 0 dead ahead.</summary>
        public float Bearing(Vector3 shipPosition, Quaternion shipRotation, Vector3 destination)
        {
            Vector3 toDestination = Level(destination - shipPosition);
            if (toDestination.sqrMagnitude < 1e-6f) return 0f;
            return Vector3.SignedAngle(LevelForward(shipRotation), toDestination, Vector3.up);
        }

        private static Vector3 LevelForward(Quaternion rotation)
        {
            Vector3 forward = Level(rotation * Vector3.forward);
            return forward.sqrMagnitude > 1e-6f ? forward.normalized : Vector3.forward;
        }

        private static Vector3 Level(Vector3 vector) => new(vector.x, 0f, vector.z);
    }
}
