#nullable enable
using System.Collections.Generic;
using TinCan.Core.Domain;

namespace TinCan.Features.CloudBoundary
{
    /// <summary>The replicated hole list, on the CloudHolesState ship fixture (<see cref="CloudHolesNetworkMediator"/>).
    /// Only the server writes it.</summary>
    public interface ICloudHoleState : IActor
    {
        IReadOnlyList<CloudHole> Holes { get; }

        void ServerAdd(CloudHole hole);

        void ServerRemoveAt(int index);
    }
}
