#nullable enable

namespace TinCan.Features.CloudBoundary
{
    /// <summary>The cloud surface's height at a world point (<see cref="CloudSurfaceQuery"/>).</summary>
    public interface ICloudSurfaceQuery
    {
        float GetSurfaceHeight(float worldX, float worldZ);
    }
}
