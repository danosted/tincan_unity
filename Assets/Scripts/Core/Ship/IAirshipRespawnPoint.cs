#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>Where players board a ship: joiners and the cloud reset land here (<see cref="AirshipBoardingPose"/>).</summary>
    public interface IAirshipRespawnPoint
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
    }
}
