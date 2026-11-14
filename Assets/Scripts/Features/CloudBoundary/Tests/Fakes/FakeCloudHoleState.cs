#nullable enable
using System;
using System.Collections.Generic;
using TinCan.Features.CloudBoundary;

namespace TinCan.Features.CloudBoundary.Tests.Fakes
{
    /// <summary>The replicated hole list without the network: a plain list.</summary>
    public sealed class FakeCloudHoleState : ICloudHoleState
    {
        private readonly List<CloudHole> _holes = new();

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public IReadOnlyList<CloudHole> Holes => _holes;

        public void ServerAdd(CloudHole hole) => _holes.Add(hole);

        public void ServerRemoveAt(int index) => _holes.RemoveAt(index);
    }
}
