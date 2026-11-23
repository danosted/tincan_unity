#nullable enable

namespace TinCan.Features.Storm
{
    /// <summary>
    /// How much storm the local view is in: <see cref="Depth"/> (0 outside the wall, 1 at the eye's edge; per peer, from
    /// where the camera is) and <see cref="Strength"/> (1 raging, 0 cured). In the lab they come from
    /// <see cref="StormLabSettings"/>'s sliders, or from the encounter zone (<see cref="StormIntensity"/>).
    /// </summary>
    public interface IStormIntensity
    {
        float Depth { get; }
        float Strength { get; }
    }
}
