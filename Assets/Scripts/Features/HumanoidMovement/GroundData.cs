#nullable enable
using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Data structure representing the state of the surface under an actor's feet.
    /// </summary>
    public struct GroundData
    {
        public bool IsGrounded;
        public Transform? GroundTransform; // The transform we are currently standing on
        public Vector3 GroundNormal;
        public Vector3 GroundVelocity;
        public Vector3 SurfaceDelta; // The actual world-space movement of the ground since the last frame
        public Quaternion RotationDelta; // The actual world-space rotation of the ground since the last frame
    }
}
