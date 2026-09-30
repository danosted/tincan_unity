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

    /// <summary>How the field grows with the crew: its limit and spawn interval for one player, and what each extra player changes.</summary>
    public readonly struct SkyHazardCrewScaling
    {
        public readonly int MaxAlive;
        public readonly int MaxAlivePerExtraPlayer;
        public readonly float SpawnInterval;
        public readonly float SpawnIntervalScalePerExtraPlayer;

        public SkyHazardCrewScaling(int maxAlive, int maxAlivePerExtraPlayer, float spawnInterval, float spawnIntervalScalePerExtraPlayer)
        {
            MaxAlive = maxAlive;
            MaxAlivePerExtraPlayer = maxAlivePerExtraPlayer;
            SpawnInterval = spawnInterval;
            SpawnIntervalScalePerExtraPlayer = spawnIntervalScalePerExtraPlayer;
        }
    }

    /// <summary>
    /// Domain, pure: picks spawn points in a box around the ship, in its heading frame (level with the horizon, whatever
    /// the ship's pitch and bank), sizes the field for the crew, and says when a hazard has fallen too far behind to keep.
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

        /// <summary>
        /// The field's limit and spawn interval for a crew of <paramref name="players"/> (fewer than one counts as one):
        /// each player beyond the first raises the limit and shortens the interval.
        /// </summary>
        public (int MaxAlive, float SpawnInterval) ForCrew(int players, in SkyHazardCrewScaling scaling)
        {
            int extra = Mathf.Max(0, players - 1);
            return (scaling.MaxAlive + scaling.MaxAlivePerExtraPlayer * extra,
                scaling.SpawnInterval * Mathf.Pow(scaling.SpawnIntervalScalePerExtraPlayer, extra));
        }

        public bool IsTooFar(Vector3 shipPosition, Vector3 hazardPosition, float removeDistance) =>
            (hazardPosition - shipPosition).sqrMagnitude > removeDistance * removeDistance;
    }
}
