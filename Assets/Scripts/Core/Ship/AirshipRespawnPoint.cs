#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>
    /// Marks a ship's boarding spot. Optional: a ship without one uses the caller's ship-local fallback offset.
    /// </summary>
    public class AirshipRespawnPoint : MonoBehaviour, IAirshipRespawnPoint
    {
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
    }
}
