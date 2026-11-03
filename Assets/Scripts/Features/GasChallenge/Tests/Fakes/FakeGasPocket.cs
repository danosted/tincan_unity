#nullable enable
using System;
using TinCan.Features.GasChallenge;
using UnityEngine;

namespace TinCan.Features.GasChallenge.Tests
{
    /// <summary>A networked gas pocket without NGO: where it is, and whether it has gone off.</summary>
    public sealed class FakeGasPocket : IGasPocket
    {
        public FakeGasPocket(Vector3 center, float radius = 16f)
        {
            Center = center;
            Radius = radius;
        }

        public Guid Id { get; } = Guid.NewGuid();
        public bool IsSimulating { get; set; } = true;
        public Vector3 Center { get; }
        public float Radius { get; }
        public bool Detonated { get; set; }
        public GasPocketVolume Volume => null!;
    }
}
