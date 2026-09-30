#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Where a player boards a ship: its <see cref="IAirshipRespawnPoint"/> when it has one, else
    /// <paramref name="fallbackOffset"/> in ship-local space, facing the ship's heading.
    /// </summary>
    public static class AirshipBoardingPose
    {
        public static (Vector3 Position, Quaternion Rotation) Resolve(IAirshipView airship, Vector3 fallbackOffset)
        {
            var respawnPoint = airship.Transform.GetComponentInChildren<IAirshipRespawnPoint>(true);
            if (respawnPoint != null) return (respawnPoint.Position, respawnPoint.Rotation);

            return (airship.Transform.TransformPoint(fallbackOffset), airship.Transform.rotation);
        }
    }
}
