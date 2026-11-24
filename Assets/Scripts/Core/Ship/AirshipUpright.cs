#nullable enable
using UnityEngine;

namespace TinCan.Core.Ship
{
    /// <summary>A ship put somewhere (a respawn) stands upright, facing the way it flew: no pitch, no bank.</summary>
    public static class AirshipUpright
    {
        public static Quaternion Of(Quaternion rotation)
        {
            Vector3 forward = rotation * Vector3.forward;
            forward.y = 0f;
            return forward.sqrMagnitude < 1e-6f ? Quaternion.identity : Quaternion.LookRotation(forward.normalized, Vector3.up);
        }
    }
}
