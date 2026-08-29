#nullable enable
using System;

namespace TinCan.Features.CloudBoundary
{
    public interface ICloudBoundaryExpiryHandler
    {
        void HandleExpiry(Guid airshipId);
    }

    public class NoOpCloudBoundaryExpiryHandler : ICloudBoundaryExpiryHandler
    {
        public void HandleExpiry(Guid airshipId)
        {
        }
    }
}
