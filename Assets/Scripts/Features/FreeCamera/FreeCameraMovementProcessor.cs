using UnityEngine;

namespace TinCan.Features.FreeCamera
{
    /// <summary>
    /// Domain Layer: Pure logic for calculating free camera movement.
    /// Decoupled from Unity's Update loop and Input system.
    /// </summary>
    public class FreeCameraMovementProcessor
    {
        public Vector3 CalculateDisplacement(Vector3 direction, float speed, float deltaTime)
        {
            return direction * speed * deltaTime;
        }
    }
}
