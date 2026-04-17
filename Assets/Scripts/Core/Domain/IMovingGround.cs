using UnityEngine;

namespace TinCan.Core.Domain
{
    /// <summary>
    /// Interface for any object that moves and should carry actors standing on it.
    /// </summary>
    public interface IMovingGround
    {
        Vector3 Velocity { get; }
        Vector3 PositionDelta { get; }
        Quaternion RotationDelta { get; }
    }
}
