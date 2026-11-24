#nullable enable
using UnityEngine;

namespace TinCan.Features.Storm
{
    /// <summary>
    /// How much storm the local view is in, and where it stands: <see cref="Depth"/> (0 at the wall's foot and outside, 1
    /// at the centre; per peer, from where the camera is), <see cref="Strength"/> (1 raging, 0 cured), and the storm's
    /// <see cref="Region"/> in the world for the shaders (null: none, as in the lab's slider mode). From the nearest
    /// encounter zone, or from the lab's controls (<see cref="StormIntensity"/>). <see cref="Camera"/> is where the local
    /// view is (null: no view yet), so a strike's thunder comes later and fainter the farther it is.
    /// </summary>
    public interface IStormIntensity
    {
        float Depth { get; }
        float Strength { get; }
        (Vector2 Centre, float Radius)? Region { get; }
        Vector3? Camera { get; }
    }
}
