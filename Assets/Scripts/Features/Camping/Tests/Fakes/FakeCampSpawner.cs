#nullable enable
using System.Collections.Generic;
using UnityEngine;

namespace TinCan.Features.Camping.Tests.Fakes
{
    /// <summary>Records what was spawned; <see cref="Fails"/> makes every spawn fail (no prefab).</summary>
    public sealed class FakeCampSpawner : ICampSpawner
    {
        private ulong _next = 300;

        public List<(CampPlaceable Kind, Vector3 Position, Quaternion Rotation)> Spawned { get; } = new();
        public bool Fails { get; set; }

        public ulong? Spawn(CampPlaceable kind, Vector3 position, Quaternion rotation)
        {
            if (Fails) return null;
            Spawned.Add((kind, position, rotation));
            return _next++;
        }
    }
}
