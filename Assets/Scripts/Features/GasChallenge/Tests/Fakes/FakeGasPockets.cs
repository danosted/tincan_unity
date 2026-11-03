#nullable enable
using System.Collections.Generic;
using TinCan.Features.GasChallenge;
using UnityEngine;

namespace TinCan.Features.GasChallenge.Tests
{
    /// <summary>Spawns <see cref="FakeGasPocket"/>s and records what it spawned and despawned.</summary>
    public sealed class FakeGasPockets : IGasPocketSpawner
    {
        public List<FakeGasPocket> Spawned { get; } = new();
        public List<IGasPocket> Despawned { get; } = new();

        public IGasPocket? Spawn(Vector3 position)
        {
            var pocket = new FakeGasPocket(position);
            Spawned.Add(pocket);
            return pocket;
        }

        public void Despawn(IGasPocket pocket) => Despawned.Add(pocket);
    }
}
