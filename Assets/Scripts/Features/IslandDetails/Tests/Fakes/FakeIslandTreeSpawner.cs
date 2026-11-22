#nullable enable
using System.Collections.Generic;
using TinCan.Features.IslandDetails;
using UnityEngine;

namespace TinCan.Features.IslandDetails.Tests.Fakes
{
    /// <summary>Records spawned trees by id and the ids despawned.</summary>
    public sealed class FakeIslandTreeSpawner : IIslandTreeSpawner
    {
        private ulong _next = 500;

        public Dictionary<ulong, (Vector3 Position, Quaternion Rotation, float Scale, int Variant, IslandDetailKind Kind)> Spawned { get; } = new();
        public List<ulong> Despawned { get; } = new();

        public ulong? Spawn(IslandDetailKind kind, Vector3 position, Quaternion rotation, float scale, int variant)
        {
            ulong id = _next++;
            Spawned[id] = (position, rotation, scale, variant, kind);
            return id;
        }

        public void Despawn(ulong treeId) => Despawned.Add(treeId);
    }
}
