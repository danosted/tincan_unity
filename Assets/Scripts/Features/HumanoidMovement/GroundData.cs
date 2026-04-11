using UnityEngine;

namespace TinCan.Features.HumanoidMovement
{
    /// <summary>
    /// Domain Layer: Data structure representing the state of the surface under an actor's feet.
    /// </summary>
    public struct GroundData
    {
        public bool IsGrounded;
        public Vector3 GroundNormal;
        public Vector3 GroundVelocity;
        public Vector3 SurfaceDelta; // The actual world-space movement of the ground since the last frame
    }
}
