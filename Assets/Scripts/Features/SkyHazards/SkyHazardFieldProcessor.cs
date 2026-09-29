#nullable enable
using UnityEngine;

namespace TinCan.Features.SkyHazards
{
    /// <summary>Where hazards appear around a ship, in metres in the ship's level heading frame (x right, y up, z ahead).</summary>
    public readonly struct SkyHazardFieldShape
    {
        public readonly Vector3 Min;
        public readonly Vector3 Max;

        public SkyHazardFieldShape(Vector3 min, Vector3 max)
        {
            Min = Vector3.Min(min, max);
            Max = Vector3.Max(min, max);
        }
    }

    /// <summary>
    /// Domain, pure: picks spawn points in a box around the ship, in its heading frame (level with the horizon, whatever
    /// the ship's pitch and bank), and says when a hazard has fallen too far behind to keep.
    /// </summary>
    public class SkyHazardFieldProcessor
    {
        /// <summary>A spawn point from three uniform randoms in [0, 1): lateral, height, ahead.</summary>
        public Vector3 SpawnPoint(Vector3 shipPosition, Quaternion shipRotation, in SkyHazardFieldShape shape, float lateral01, float height01, float ahead01)
        {
            Vector3 forward = Vector3.ProjectOnPlane(shipRotation * Vector3.forward, Vector3.up);
            if (forward.sqrMagnitude < 1e-6f) forward = Vector3.forward;
            var heading = Quaternion.LookRotation(forward.normalized, Vector3.up);

            var local = new Vector3(
                Mathf.Lerp(shape.Min.x, shape.Max.x, lateral01),
                Mathf.Lerp(shape.Min.y, shape.Max.y, height01),
                Mathf.Lerp(shape.Min.z, shape.Max.z, ahead01));
            return shipPosition + heading * local;
        }

        public bool IsTooFar(Vector3 shipPosition, Vector3 hazardPosition, float removeDistance) =>
            (hazardPosition - shipPosition).sqrMagnitude > removeDistance * removeDistance;
    }
}
