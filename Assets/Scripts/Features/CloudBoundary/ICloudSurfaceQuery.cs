#nullable enable

namespace TinCan.Features.CloudBoundary
{
    /// <summary>The cloud surface's height at a world point (<see cref="CloudSurfaceQuery"/>).</summary>
    public interface ICloudSurfaceQuery
    {
        /// <summary>The surface as drawn: the sea, dropping deep into any open hole or rift.</summary>
        float GetSurfaceHeight(float worldX, float worldZ);

        /// <summary>The sea's level there, as if no hole or rift were open: over a hole, the level of the sea around it.</summary>
        float GetSeaHeight(float worldX, float worldZ);
    }
}
