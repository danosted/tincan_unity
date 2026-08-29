#nullable enable
using UnityEngine;

namespace TinCan.Features.CloudBoundary
{
    public interface IAirshipRespawnPoint
    {
        Vector3 Position { get; }
        Quaternion Rotation { get; }
    }

    public class AirshipRespawnPoint : MonoBehaviour, IAirshipRespawnPoint
    {
        public Vector3 Position => transform.position;
        public Quaternion Rotation => transform.rotation;
    }
}
